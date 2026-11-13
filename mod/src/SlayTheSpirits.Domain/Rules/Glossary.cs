using System.Globalization;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>A keyword tooltip: title and body text.</summary>
public sealed record GlossaryEntry(string Title, string Body);

/// <summary>
/// Keyword tooltips for penalties and Tipsy, generated from the rules in effect so they always state the
/// configured values (labels and Tipsy weights come from the host's rules).
/// </summary>
public static class Glossary
{
    public const string TipsyTitle = "Tipsy";

    private static readonly Dictionary<PenaltyKind, string> Actions = new()
    {
        [PenaltyKind.Water] = "Drink some water.",
        [PenaltyKind.Sip] = "Take a sip.",
        [PenaltyKind.Drink] = "Take a proper drink.",
        [PenaltyKind.Shot] = "Take a shot.",
        [PenaltyKind.Chug] = "Chug for 3 seconds.",
        [PenaltyKind.Finish] = "Finish whatever is in your glass.",
    };

    /// <summary>E.g. Title "Shot", Body "Take a shot. Gain 3 Tipsy."</summary>
    public static GlossaryEntry For(RuleSet rules, PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var label = PenaltyLabels.For(rules, kind).Singular;
        var tipsy = TipsyWeights.PerUnit(rules, kind);
        var action = rules.Labels.ContainsKey(kind) ? $"{label}." : Actions[kind];
        var gain = tipsy > 0
            ? string.Create(CultureInfo.InvariantCulture, $" Gain {tipsy} {TipsyTitle}.")
            : string.Empty;
        return new GlossaryEntry(label, action + gain);
    }

    /// <summary>
    /// A bar tab counter: how many of one drink a player has been handed this combat, so players can check
    /// they've kept up. E.g. Title "Shots", Body "Shots handed to this player this combat. Keep count as you drink!"
    /// </summary>
    public static GlossaryEntry Tab(RuleSet rules, PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var plural = PenaltyLabels.For(rules, kind).Plural;
        return new GlossaryEntry(plural, $"{plural} handed to this player this combat. Keep count as you drink!");
    }

    /// <summary>The Tipsy buff itself.</summary>
    public static GlossaryEntry Tipsy(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var scale = string.Join(", ", Enum.GetValues<PenaltyKind>()
            .Where(k => TipsyWeights.PerUnit(rules, k) > 0)
            .Select(k => string.Create(CultureInfo.InvariantCulture, $"{PenaltyLabels.For(rules, k).Singular} {TipsyWeights.PerUnit(rules, k)}")));
        return new GlossaryEntry(TipsyTitle, $"How much this player has had to drink this run. Some Tavern cards grow stronger with Tipsy.\n({scale})");
    }
}
