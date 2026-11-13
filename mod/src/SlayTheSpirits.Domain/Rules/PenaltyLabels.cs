using System.Globalization;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>Formats penalties for display, honoring a rule set's custom labels.</summary>
public static class PenaltyLabels
{
    public static IReadOnlyDictionary<PenaltyKind, PenaltyLabel> Defaults { get; } = new Dictionary<PenaltyKind, PenaltyLabel>
    {
        [PenaltyKind.Water] = new("Water", "Waters"),
        [PenaltyKind.Sip] = new("Sip", "Sips"),
        [PenaltyKind.Drink] = new("Drink", "Drinks"),
        [PenaltyKind.Shot] = new("Shot", "Shots"),
        [PenaltyKind.Chug] = new("Chug", "Chugs"),
        [PenaltyKind.Finish] = new("Finish Your Drink", "Finish Your Drink"),
    };

    public static PenaltyLabel For(RuleSet rules, PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Labels.GetValueOrDefault(kind) ?? Defaults[kind];
    }

    /// <summary>"Shot", "3 Shots".</summary>
    public static string Describe(RuleSet rules, Penalty penalty)
    {
        var label = For(rules, penalty.Kind);
        return penalty.Count == 1
            ? label.Singular
            : string.Create(CultureInfo.InvariantCulture, $"{penalty.Count} {label.Plural}");
    }
}
