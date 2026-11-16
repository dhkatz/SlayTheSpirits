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

    /// <summary>Per player: rest site tabs taken this act (<see cref="RestSiteTabs"/>).</summary>
    public Dictionary<ulong, Dictionary<RestSiteTab, int>> RestSiteTabsThisAct { get; init; } = [];

    /// <summary>Players currently immune (e.g. Designated Driver). Cleared by the game layer at combat end.</summary>
    public HashSet<ulong> Immune { get; init; } = [];

    /// <summary>Per player: number of upcoming penalties to pass to a random other player (e.g. Slip Away).</summary>
    public Dictionary<ulong, int> PendingRedirects { get; init; } = [];

    /// <summary>
    /// Per player: the last batch of drinks that was redirected away from them, and to whom, so the rest of that batch
    /// follows (<see cref="PenaltyRequest.Batch"/>). A batch is one card play, which never spans a save, so this isn't
    /// persisted.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<ulong, (string Batch, PlayerId Target)> RedirectedBatches { get; } = [];

    /// <summary>Run-long totals and highlights.</summary>
    public RunTally Tally { get; init; } = new();

    public int CountThisAct(PlayerId player, PenaltyKind kind) =>
        ActCounts.TryGetValue(player.Value, out var counts) ? counts.GetValueOrDefault(kind) : 0;

    public int RestSiteTabCount(PlayerId player, RestSiteTab tab) =>
        RestSiteTabsThisAct.TryGetValue(player.Value, out var counts) ? counts.GetValueOrDefault(tab) : 0;

    internal void RecordRestSiteTab(PlayerId player, RestSiteTab tab)
    {
        if (!RestSiteTabsThisAct.TryGetValue(player.Value, out var counts))
        {
            counts = [];
            RestSiteTabsThisAct[player.Value] = counts;
        }

        counts[tab] = counts.GetValueOrDefault(tab) + 1;
    }

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
