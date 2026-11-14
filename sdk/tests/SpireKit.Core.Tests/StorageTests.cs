using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SpireKit.Sharing;
using SpireKit.Storage;

namespace SpireKit.Tests;

public sealed record Settings(string Name, int Volume);

/// <summary>Init-only settings with non-trivial defaults, the shape that loses defaults without filling.</summary>
public sealed record Prefs
{
    public string Name { get; init; } = "default";

    public bool Enabled { get; init; } = true;

    public int Volume { get; init; } = 7;

    public PrefsSection Section { get; init; } = new();

    public IReadOnlyDictionary<string, int> Weights { get; init; } = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
}

public sealed record PrefsSection
{
    public bool Loud { get; init; } = true;

    public string Mode { get; init; } = "normal";
}

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(Prefs))]
internal sealed partial class TestJsonContext : JsonSerializerContext;

public sealed class StorageTests
{
    private static readonly IJsonMigration RenameLoudness = new JsonMigration(1, data =>
    {
        var obj = data.AsObject();
        obj["Volume"] = obj["Loudness"]!.DeepClone();
        obj.Remove("Loudness");
        return obj;
    });

    private static VersionedJsonDocument<Settings> Format(Func<Settings, IReadOnlyList<string>>? validate = null) =>
        new(2, TestJsonContext.Default.Settings, [RenameLoudness], validate);

    [Fact]
    public void Round_trips_at_current_version()
    {
        var format = Format();
        var result = format.Decode(format.Encode(new Settings("a", 3)), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal(new Settings("a", 3), result.Value);
    }

    [Fact]
    public void Migrates_old_payloads()
    {
        var v1 = new JsonObject { ["schema"] = 1, ["data"] = new JsonObject { ["Name"] = "old", ["Loudness"] = 9 } };

        var result = Format().Decode(v1.ToJsonString(), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Migrated, result.Status);
        Assert.Equal(new Settings("old", 9), result.Value);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schema\": 99, \"data\": {}}")]
    [InlineData("{\"data\": {}}")]
    public void Falls_back_on_unreadable_input(string json)
    {
        var result = Format().Decode(json, () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Invalid, result.Status);
        Assert.Equal("default", result.Value.Name);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Falls_back_when_validation_fails()
    {
        var format = Format(s => s.Volume > 10 ? ["too loud"] : []);

        var result = format.Decode(format.Encode(new Settings("x", 11)), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Invalid, result.Status);
        Assert.Contains("too loud", result.Error, StringComparison.Ordinal);
    }

    private static VersionedJsonDocument<Prefs> PrefsFormat() =>
        new(1, TestJsonContext.Default.Prefs, defaults: static () => new Prefs());

    [Fact]
    public void Missing_properties_take_the_defaults_instead_of_null_or_false()
    {
        const string json = """{"schema":1,"data":{"Name":"mine","Section":{"Mode":"quiet"}}}""";

        var result = PrefsFormat().Decode(json, static () => throw new InvalidOperationException("no fallback"));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal("mine", result.Value.Name);
        Assert.True(result.Value.Enabled);
        Assert.Equal(7, result.Value.Volume);
        Assert.True(result.Value.Section.Loud);
        Assert.Equal("quiet", result.Value.Section.Mode);
        Assert.Equal(new Prefs().Weights, result.Value.Weights);
    }

    [Fact]
    public void Present_values_win_and_dictionaries_are_not_merged()
    {
        const string json = """{"schema":1,"data":{"Enabled":false,"Volume":0,"Weights":{"c":3},"Section":null}}""";

        var result = PrefsFormat().Decode(json, static () => throw new InvalidOperationException("no fallback"));

        Assert.False(result.Value.Enabled);
        Assert.Equal(0, result.Value.Volume);
        Assert.Equal(new Dictionary<string, int> { ["c"] = 3 }, result.Value.Weights);
        Assert.Equal(new PrefsSection(), result.Value.Section); // Null counts as missing.
    }

    [Fact]
    public void A_throwing_validator_falls_back_instead_of_crashing()
    {
        var format = Format(_ => throw new KeyNotFoundException("bug in validator"));

        var result = format.Decode(format.Encode(new Settings("a", 3)), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Invalid, result.Status);
        Assert.Equal("default", result.Value.Name);
    }

    [Fact]
    public void Document_file_creates_defaults_and_preserves_corrupt_files()
    {
        var store = new InMemoryFileStore();
        var file = new JsonDocumentFile<Settings>(store, "cfg/settings.json", Format(), () => new Settings("default", 1));

        Assert.Equal("default", file.Load().Name);
        Assert.NotNull(store.ReadText("cfg/settings.json"));

        store.WriteText("cfg/settings.json", "{ broken");
        Assert.Equal("default", file.Load().Name);
        Assert.Equal("{ broken", store.ReadText("cfg/settings.json.corrupt-1"));
    }

    [Fact]
    public void File_store_rejects_escaping_paths()
    {
        var store = new InMemoryFileStore();

        Assert.Throws<ArgumentException>(() => store.WriteText("../evil.json", "x"));
        Assert.Throws<ArgumentException>(() => store.WriteText("C:/evil.json", "x"));
    }

    [Fact]
    public void Share_codes_round_trip_and_reject_foreign_input()
    {
        var code = ShareCode.Encode("TEST1", "{\"hello\":\"world\"}");

        Assert.StartsWith("TEST1:", code, StringComparison.Ordinal);
        Assert.True(ShareCode.TryDecode("TEST1", $"  {code}\n", out var payload));
        Assert.Equal("{\"hello\":\"world\"}", payload);
        Assert.False(ShareCode.TryDecode("OTHER1", code, out _));
        Assert.False(ShareCode.TryDecode("TEST1", "TEST1:!!!", out _));
        Assert.False(ShareCode.TryDecode("TEST1", null, out _));
    }
}
