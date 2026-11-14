using System.Globalization;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SpireKit.Localization;

namespace SlayTheSpirits.Presentation;

/// <summary>
/// Text for the rest site drink options, generated from the rules in effect so it states the configured drink names
/// and Tipsy (and the right limit). Re-applied whenever the host's rules change.
/// </summary>
internal static class RestSiteText
{
    public static void Apply(LocInjector loc, RuleSet? rules)
    {
        rules ??= Presets.Standard;
        Set(loc, rules, SpiritsChugRestSiteOption.Id, PenaltyKind.Chug, PenaltyLabels.For(rules, PenaltyKind.Chug).Singular,
            string.Create(CultureInfo.InvariantCulture, $"Then choose {SpiritsChugRestSiteOption.Picked} of {SpiritsChugRestSiteOption.Offered} Tavern cards to add to your deck."));
        Set(loc, rules, SpiritsShotRestSiteOption.Id, PenaltyKind.Shot, $"Take a {PenaltyLabels.For(rules, PenaltyKind.Shot).Singular}",
            "Then choose a Tavern relic, found only here.");
        loc.Set(SpiritsRestSiteDrink.Table, $"{SpiritsChugRestSiteOption.Id}.selectionScreenPrompt",
            string.Create(CultureInfo.InvariantCulture, $"Choose {SpiritsChugRestSiteOption.Picked} Tavern cards to add to your deck."));
    }

    private static void Set(LocInjector loc, RuleSet rules, string id, PenaltyKind kind, string name, string reward)
    {
        var drink = Glossary.For(rules, kind).Body; // E.g. "Take a shot of liquor. Gain 10 Tipsy."
        var label = PenaltyLabels.For(rules, kind).Singular;

        loc.Set(SpiritsRestSiteDrink.Table, $"OPTION_{id}.name", name);
        loc.Set(SpiritsRestSiteDrink.Table, $"OPTION_{id}.description", $"{drink} {reward}");
        // Only shown under the once-per-act limit.
        loc.Set(SpiritsRestSiteDrink.Table, $"OPTION_{id}.descriptionDisabled", $"You've had your rest site {label} for this act. It's back next act.");
    }
}
