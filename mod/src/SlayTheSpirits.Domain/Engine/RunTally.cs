using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Engine;

/// <summary>Run-long bookkeeping: Tipsy (the buff) and the end-of-run Bar Tab.</summary>
public sealed class RunTally
{
    /// <summary>Per player totals by kind.</summary>
    public Dictionary<ulong, Dictionary<PenaltyKind, int>> Totals { get; init; } = [];

    /// <summary>Per player Tipsy accumulated this run.</summary>
    public Dictionary<ulong, int> Tipsy { get; init; } = [];

    /// <summary>Per player: their single heaviest penalty (first one wins ties).</summary>
    public Dictionary<ulong, TallyHighlight> WorstMoments { get; init; } = [];

    public int TipsyOf(PlayerId player) => Tipsy.GetValueOrDefault(player.Value);

    public int Count(PlayerId player, PenaltyKind kind) =>
        Totals.TryGetValue(player.Value, out var byKind) ? byKind.GetValueOrDefault(kind) : 0;

    internal void Add(PenaltyAssignment assignment)
    {
        var key = assignment.Player.Value;
        if (!Totals.TryGetValue(key, out var byKind))
        {
            byKind = [];
            Totals[key] = byKind;
        }

        byKind[assignment.Penalty.Kind] = byKind.GetValueOrDefault(assignment.Penalty.Kind) + assignment.Penalty.Count;
        Tipsy[key] = Tipsy.GetValueOrDefault(key) + assignment.Tipsy;

        if (!WorstMoments.TryGetValue(key, out var worst) || Severity(assignment.Penalty) > Severity(worst.Penalty))
        {
            WorstMoments[key] = new TallyHighlight(assignment.Penalty, assignment.Cause.SourceId, assignment.Cause.Label);
        }
    }

    /// <summary>Orders penalties for "worst moment": heavier kind first, then larger count.</summary>
    private static int Severity(Penalty penalty) => ((int)penalty.Kind * (Penalty.MaxCount + 1)) + penalty.Count;
}

/// <summary>A memorable penalty for the Bar Tab.</summary>
public sealed record TallyHighlight(Penalty Penalty, string SourceId, string? Label);
