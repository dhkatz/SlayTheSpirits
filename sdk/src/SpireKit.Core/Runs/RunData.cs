using System.Text.Json;
using System.Text.Json.Nodes;
using SpireKit.Logging;

namespace SpireKit.Runs;

/// <summary>
/// Identifies one run across saves, loads and peers: the run's seed plus its start time (two runs can share
/// a seed, e.g. a replayed custom seed).
/// </summary>
public readonly record struct RunIdentity(string Seed, long StartTime)
{
    public override string ToString() => $"{Seed}@{StartTime}";
}

/// <summary>
/// A mod's extra data for one run: named text payloads ("slots"), stamped with the run they belong to.
/// </summary>
public sealed record RunDataBundle(RunIdentity Run, IReadOnlyDictionary<string, string> Slots)
{
    private const int FormatVersion = 1;

    public string Encode()
    {
        var slots = new JsonObject();
        foreach (var (name, payload) in Slots.OrderBy(static s => s.Key, StringComparer.Ordinal))
        {
            slots[name] = payload;
        }

        return new JsonObject
        {
            ["format"] = FormatVersion,
            ["seed"] = Run.Seed,
            ["startTime"] = Run.StartTime,
            ["slots"] = slots,
        }.ToJsonString();
    }

    /// <summary>Decodes a bundle, or returns null for empty, corrupt or newer-format input.</summary>
    public static RunDataBundle? TryDecode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(text) is not JsonObject root
                || root["format"]?.GetValue<int>() is not FormatVersion
                || root["seed"]?.GetValue<string>() is not { } seed
                || root["startTime"]?.GetValue<long>() is not { } startTime
                || root["slots"] is not JsonObject slotsNode)
            {
                return null;
            }

            var slots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, value) in slotsNode)
            {
                if (value?.GetValue<string>() is { } payload)
                {
                    slots[name] = payload;
                }
            }

            return new RunDataBundle(new RunIdentity(seed, startTime), slots);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// Data a mod keeps per run that must survive save/quit and reach every peer when a saved multiplayer run is
/// loaded. Each slot captures its state as text when the game snapshots the run, and receives it back when a
/// saved run is set up (before any of the run's hooks fire).
/// </summary>
/// <remarks>
/// Pure: the game-bound layer decides when to capture (the game's own save snapshot), where to store bundles
/// (beside the game's save file) and how they travel (appended to the save the host sends to clients).
/// Capture happens at exactly the moment the game snapshots the run, so a resumed run and its mod data
/// always describe the same point in time.
/// </remarks>
public sealed class RunDataRegistry(IModLog? log = null)
{
    private readonly IModLog _log = log ?? NullModLog.Instance;
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private readonly Dictionary<RunIdentity, RunDataBundle> _known = [];

    /// <summary>
    /// Registers a slot. <paramref name="capture"/> returns the current state (null = nothing to save);
    /// <paramref name="restore"/> receives the saved state when a saved run is set up, or null if there was none.
    /// </summary>
    public void Register(string name, Func<string?> capture, Action<string?> restore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(restore);
        if (!_slots.TryAdd(name, new Slot(capture, restore)))
        {
            throw new InvalidOperationException($"Run data slot '{name}' is already registered.");
        }
    }

    public bool HasSlots => _slots.Count > 0;

    /// <summary>Snapshots every slot for <paramref name="run"/> and remembers the bundle for that run.</summary>
    public RunDataBundle Capture(RunIdentity run)
    {
        var payloads = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, slot) in _slots)
        {
            try
            {
                if (slot.Capture() is { } payload)
                {
                    payloads[name] = payload;
                }
            }
#pragma warning disable CA1031 // One broken slot must not break the game's save.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.Error($"Run data slot '{name}' failed to capture; it won't be saved this time.", ex);
            }
        }

        var bundle = new RunDataBundle(run, payloads);
        _known[run] = bundle;
        return bundle;
    }

    /// <summary>Remembers a bundle read from disk or received from the host.</summary>
    public void Remember(RunDataBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        _known[bundle.Run] = bundle;
    }

    /// <summary>The latest bundle known for a run (captured, loaded or received), if any.</summary>
    public RunDataBundle? Find(RunIdentity run) => _known.GetValueOrDefault(run);

    /// <summary>A saved run is being set up: hands every slot its payload (null if it has none).</summary>
    public void Restore(RunIdentity run)
    {
        var bundle = Find(run);
        _log.Info(bundle is null
            ? $"No saved mod data for run {run}; starting fresh."
            : $"Restoring mod data for run {run} ({bundle.Slots.Count} slot(s)).");
        foreach (var (name, slot) in _slots)
        {
            try
            {
                slot.Restore(bundle?.Slots.GetValueOrDefault(name));
            }
#pragma warning disable CA1031 // One broken slot must not stop the others or the game's load.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _log.Error($"Run data slot '{name}' failed to restore.", ex);
            }
        }
    }

    private sealed record Slot(Func<string?> Capture, Action<string?> Restore);
}
