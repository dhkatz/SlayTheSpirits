using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using SpireKit.Logging;

namespace SpireKit.Storage;

/// <summary>Upgrades a document's JSON payload from <see cref="FromVersion"/> to <c>FromVersion + 1</c>.</summary>
public interface IJsonMigration
{
    int FromVersion { get; }

    JsonNode Migrate(JsonNode data);
}

/// <summary>A migration expressed as a delegate.</summary>
public sealed record JsonMigration(int FromVersion, Func<JsonNode, JsonNode> Apply) : IJsonMigration
{
    public JsonNode Migrate(JsonNode data) => Apply(data);
}

/// <summary>Outcome of <see cref="VersionedJsonDocument{T}.Decode"/>.</summary>
public enum DecodeStatus
{
    /// <summary>Decoded at the current schema version.</summary>
    Ok,

    /// <summary>Decoded after running one or more migrations.</summary>
    Migrated,

    /// <summary>Input was unreadable, from the future, or failed validation; the fallback was used.</summary>
    Invalid,
}

/// <summary>Result of decoding a versioned document.</summary>
public sealed record DecodeResult<T>(T Value, DecodeStatus Status, string? Error = null);

/// <summary>
/// Encodes/decodes a value as a schema-versioned JSON envelope: <c>{"schema": N, "data": {...}}</c>.
/// Old payloads are upgraded through registered <see cref="IJsonMigration"/>s before deserialization.
/// </summary>
/// <remarks>
/// <para>
/// Pass <c>defaults</c> whenever <typeparamref name="T"/> has init-only properties with initializers. System.Text.Json's
/// source generator sets every init-only property on construction, so a property missing from the JSON becomes
/// null/false/0 instead of keeping its initializer. With <c>defaults</c>, missing (or null) properties are filled
/// from the serialized default instance first, recursing into nested objects but not into dictionaries or lists,
/// which are taken whole. That makes adding a setting a non-breaking change that needs no migration.
/// </para>
/// <para>
/// Pure and stateless apart from configuration, so it serves both on-disk files
/// (<see cref="JsonDocumentFile{T}"/>) and network payloads.
/// </para>
/// </remarks>
public sealed class VersionedJsonDocument<T>
    where T : class
{
    private const string SchemaKey = "schema";
    private const string DataKey = "data";

    private readonly JsonTypeInfo<T> _typeInfo;
    private readonly SortedDictionary<int, IJsonMigration> _migrations = [];
    private readonly Func<T, IReadOnlyList<string>>? _validate;
    private readonly Func<T>? _defaults;

    public VersionedJsonDocument(
        int currentVersion,
        JsonTypeInfo<T> typeInfo,
        IEnumerable<IJsonMigration>? migrations = null,
        Func<T, IReadOnlyList<string>>? validate = null,
        Func<T>? defaults = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(currentVersion, 1);
        ArgumentNullException.ThrowIfNull(typeInfo);
        CurrentVersion = currentVersion;
        _typeInfo = typeInfo;
        _validate = validate;
        _defaults = defaults;
        foreach (var migration in migrations ?? [])
        {
            if (migration.FromVersion < 1 || migration.FromVersion >= currentVersion)
            {
                throw new ArgumentException($"Migration from v{migration.FromVersion} is outside [1, {currentVersion}).", nameof(migrations));
            }

            if (!_migrations.TryAdd(migration.FromVersion, migration))
            {
                throw new ArgumentException($"Duplicate migration from v{migration.FromVersion}.", nameof(migrations));
            }
        }
    }

    public int CurrentVersion { get; }

    public string Encode(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var envelope = new JsonObject
        {
            [SchemaKey] = CurrentVersion,
            [DataKey] = JsonSerializer.SerializeToNode(value, _typeInfo),
        };
        return envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public DecodeResult<T> Decode(string json, Func<T> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        try
        {
            if (JsonNode.Parse(json) is not JsonObject envelope
                || envelope[SchemaKey]?.GetValue<int>() is not { } version
                || envelope[DataKey] is not { } data)
            {
                return new(fallback(), DecodeStatus.Invalid, "Missing 'schema' or 'data'.");
            }

            if (version > CurrentVersion)
            {
                return new(fallback(), DecodeStatus.Invalid, $"Schema v{version} is newer than supported v{CurrentVersion}.");
            }

            var migrated = version < CurrentVersion;
            data = data.DeepClone();
            for (var v = version; v < CurrentVersion; v++)
            {
                if (!_migrations.TryGetValue(v, out var migration))
                {
                    return new(fallback(), DecodeStatus.Invalid, $"No migration registered from v{v}.");
                }

                data = migration.Migrate(data);
            }

            if (_defaults is not null)
            {
                JsonDefaults.Fill(data, JsonSerializer.SerializeToNode(_defaults(), _typeInfo), _typeInfo);
            }

            var value = data.Deserialize(_typeInfo);
            if (value is null)
            {
                return new(fallback(), DecodeStatus.Invalid, "Payload deserialized to null.");
            }

            IReadOnlyList<string> problems;
            try
            {
                problems = _validate?.Invoke(value) ?? [];
            }
#pragma warning disable CA1031 // A buggy validator must not take the caller down with it; treat as invalid.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                return new(fallback(), DecodeStatus.Invalid, $"Validation threw {ex.GetType().Name}: {ex.Message}");
            }

            if (problems.Count > 0)
            {
                return new(fallback(), DecodeStatus.Invalid, "Validation failed: " + string.Join("; ", problems));
            }

            return new(value, migrated ? DecodeStatus.Migrated : DecodeStatus.Ok);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NotSupportedException)
        {
            return new(fallback(), DecodeStatus.Invalid, ex.Message);
        }
    }
}

/// <summary>Fills properties missing from a JSON payload with those of a default instance.</summary>
internal static class JsonDefaults
{
    public static void Fill(JsonNode? data, JsonNode? defaults, JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || data is not JsonObject target || defaults is not JsonObject source)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (!source.TryGetPropertyValue(property.Name, out var fallback) || fallback is null)
            {
                continue;
            }

            if (!target.TryGetPropertyValue(property.Name, out var current) || current is null)
            {
                target[property.Name] = fallback.DeepClone();
            }
            else
            {
                Fill(current, fallback, typeInfo.Options.GetTypeInfo(property.PropertyType));
            }
        }
    }
}

/// <summary>A <see cref="VersionedJsonDocument{T}"/> persisted to a file in an <see cref="IFileStore"/>.</summary>
/// <remarks>
/// Unreadable files are never silently overwritten: they're copied to <c>&lt;name&gt;.corrupt-N</c>
/// before defaults are used, so a user's hand-edited config can be recovered.
/// </remarks>
public sealed class JsonDocumentFile<T>(IFileStore store, string path, VersionedJsonDocument<T> format, Func<T> createDefault, IModLog? log = null)
    where T : class
{
    private readonly IModLog _log = log ?? NullModLog.Instance;

    public string Path { get; } = FileStorePaths.Normalize(path);

    /// <summary>Loads the document, creating it with defaults if missing.</summary>
    public T Load()
    {
        var text = store.ReadText(Path);
        if (text is null)
        {
            var created = createDefault();
            Save(created);
            return created;
        }

        var result = format.Decode(text, createDefault);
        switch (result.Status)
        {
            case DecodeStatus.Migrated:
                _log.Info($"Migrated '{Path}' to schema v{format.CurrentVersion}.");
                Save(result.Value);
                break;
            case DecodeStatus.Invalid:
                var backup = NextBackupPath();
                store.WriteText(backup, text);
                _log.Warn($"Could not read '{Path}' ({result.Error}). Using defaults; original kept at '{backup}'.");
                Save(result.Value);
                break;
        }

        return result.Value;
    }

    public void Save(T value) => store.WriteText(Path, format.Encode(value));

    private string NextBackupPath()
    {
        for (var i = 1; ; i++)
        {
            var candidate = $"{Path}.corrupt-{i}";
            if (store.ReadText(candidate) is null)
            {
                return candidate;
            }
        }
    }
}
