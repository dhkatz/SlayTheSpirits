using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>How much Tipsy each penalty is worth.</summary>
public static class TipsyWeights
{
    /// <summary>Upper bound per unit, to keep Tipsy-scaling cards sane.</summary>
    public const int MaxPerUnit = 20;

    /// <summary>
    /// Roughly how many sips each drink is worth, with a can at about 12 sips: a Finish is about half a can, a Chug a
    /// whole one, a Shot a full standard drink. The heavy drinks are rare, so they're worth a lot.
    /// </summary>
    /// <remarks>Schema v3 and earlier defaulted to 1 / 3 / 5 / 10 (see <see cref="TipsyWeightsMigration"/>).</remarks>
    public static IReadOnlyDictionary<PenaltyKind, int> Defaults { get; } = new Dictionary<PenaltyKind, int>
    {
        [PenaltyKind.Water] = 0,
        [PenaltyKind.Sip] = 1,
        [PenaltyKind.Finish] = 6,
        [PenaltyKind.Chug] = 8,
        [PenaltyKind.Shot] = 10,
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
