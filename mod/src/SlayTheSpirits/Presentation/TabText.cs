using SlayTheSpirits.Penalties;
using SlayTheSpirits.Powers;
using SlayTheSpirits.Rules;
using SpireKit.Localization;

namespace SlayTheSpirits.Presentation;

/// <summary>
/// Localization for the bar tab powers, generated from the rules in effect ("Shots", or "Shots of pickle
/// juice" under the Sober preset). Re-applied whenever the host's rules change.
/// </summary>
internal static class TabText
{
    public static void Apply(LocInjector loc, RuleSet? rules)
    {
        rules ??= Presets.Standard;
        foreach (var kind in Enum.GetValues<PenaltyKind>())
        {
            var entry = Glossary.Tab(rules, kind);
            var key = EntryOf(kind);
            loc.Set("powers", $"{key}.title", entry.Title);
            loc.Set("powers", $"{key}.description", entry.Body);
            loc.Set("powers", $"{key}.smartDescription", $"[gold]{entry.Title}[/gold] handed to {{OwnerName}} this combat: [blue]{{Amount}}[/blue].\nKeep count as you drink!");
        }
    }

    /// <summary>Model entry of the tab power for a kind, e.g. <c>SPIRITS_SHOT_TAB_POWER</c>.</summary>
    private static string EntryOf(PenaltyKind kind) => $"SPIRITS_{kind.ToString().ToUpperInvariant()}_TAB_POWER";
}
