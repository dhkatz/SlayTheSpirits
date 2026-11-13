using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Engine;

/// <summary>
/// Everything the engine remembers during a run. Persisted alongside the game's run save so a
/// resumed run continues with identical caps, redirects, RNG position and tally on every peer.
/// </summary>
public sealed class RunPenaltyState
{
    /// <summary>Current act (1-based). Caps reset when it changes.</summary>
    public int Act { get; set; } = 1;

    /// <summary>Values drawn from the engine's RNG stream so far.</summary>
    public long RngDraws { get; set; }

    /// <summary>Per player: count of each kind received this act (after policies), for caps.</summary>
    public Dictionary<ulong, Dictionary<PenaltyKind, int>> ActCounts { get; init; } = [];

    /// <summary>Players currently immune (e.g. Designated Driver). Cleared by the game layer at combat end.</summary>
    public HashSet<ulong> Immune { get; init; } = [];

    /// <summary>Per player: number of upcoming penalties to pass to a random other player (e.g. Slip Away).</summary>
    public Dictionary<ulong, int> PendingRedirects { get; init; } = [];

    /// <summary>Run-long totals and highlights.</summary>
    public RunTally Tally { get; init; } = new();

    internal int CountThisAct(PlayerId player, PenaltyKind kind) =>
        ActCounts.TryGetValue(player.Value, out var counts) ? counts.GetValueOrDefault(kind) : 0;

    internal void Record(PlayerId player, Penalty penalty)
    {
        if (!ActCounts.TryGetValue(player.Value, out var counts))
        {
            counts = [];
            ActCounts[player.Value] = counts;
        }

        counts[penalty.Kind] = counts.GetValueOrDefault(penalty.Kind) + penalty.Count;
    }
}
