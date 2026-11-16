using System.Globalization;
using SlayTheSpirits.Game;
using SpireKit.Localization;

namespace SlayTheSpirits.Presentation;

/// <summary>Text for the rest site tabs (<see cref="SpiritsRestSiteTab"/>).</summary>
internal static class RestSiteText
{
    public static void Apply(LocInjector loc)
    {
        Set(loc, SpiritsOpenTabRestSiteOption.Id, "Open a Tab",
            string.Create(CultureInfo.InvariantCulture, $"Choose {SpiritsOpenTabRestSiteOption.Picked} of {SpiritsOpenTabRestSiteOption.Offered} Tavern cards to add to your deck. Put it on your tab: a random minor Tavern curse goes in your deck too."),
            "You've opened a tab at a rest site this act. It's back next act.");
        Set(loc, SpiritsRunUpTabRestSiteOption.Id, "Run Up the Tab",
            "Choose a Tavern relic. Put it on your tab: a random major Tavern curse goes in your deck too.",
            "You've run up a tab at a rest site this act. It's back next act.");
        loc.Set(SpiritsRestSiteTab.Table, $"{SpiritsOpenTabRestSiteOption.Id}.selectionScreenPrompt",
            string.Create(CultureInfo.InvariantCulture, $"Choose {SpiritsOpenTabRestSiteOption.Picked} Tavern cards to add to your deck."));
    }

    private static void Set(LocInjector loc, string id, string name, string description, string disabled)
    {
        loc.Set(SpiritsRestSiteTab.Table, $"OPTION_{id}.name", name);
        loc.Set(SpiritsRestSiteTab.Table, $"OPTION_{id}.description", description);
        // Only shown under the once-per-act limit.
        loc.Set(SpiritsRestSiteTab.Table, $"OPTION_{id}.descriptionDisabled", disabled);
    }
}
