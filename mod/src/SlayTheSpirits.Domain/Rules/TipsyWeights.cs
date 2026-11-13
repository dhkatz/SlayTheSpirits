using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>How much Tipsy each penalty is worth.</summary>
public static class TipsyWeights
{
    /// <summary>Upper bound per unit, to keep Tipsy-scaling cards sane.</summary>
    public const int MaxPerUnit = 20;

    public static IReadOnlyDictionary<PenaltyKind, int> Defaults { get; } = new Dictionary<PenaltyKind, int>
    {
        [PenaltyKind.Water] = 0,
        [PenaltyKind.Sip] = 1,
        [PenaltyKind.Drink] = 2,
        [PenaltyKind.Shot] = 3,
        [PenaltyKind.Chug] = 4,
        [PenaltyKind.Finish] = 5,
    };

    /// <summary>Tipsy per single unit of <paramref name="kind"/> under <paramref name="rules"/>.</summary>
    public static int PerUnit(RuleSet rules, PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Tipsy.TryGetValue(kind, out var weight) ? weight : Defaults[kind];
    }

    /// <summary>Total Tipsy for <paramref name="penalty"/> (weight × count).</summary>
    public static int For(RuleSet rules, Penalty penalty) => PerUnit(rules, penalty.Kind) * penalty.Count;
}
