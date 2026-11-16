using SlayTheSpirits.Party;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Engine;

/// <summary>The rest site options that put a reward on your tab: you pay for it with a Tavern curse in your deck.</summary>
public enum RestSiteTab
{
    /// <summary>Choose Tavern cards; costs a minor Tavern curse.</summary>
    Open,

    /// <summary>Choose a Tavern relic; costs a major Tavern curse.</summary>
    RunUp,
}

/// <summary>
/// Rest site tabs: a reward now, and a curse in your deck that makes you drink and plays against you later. How often
/// they're offered is <see cref="RuleSet.RestSiteTabs"/>; they're counted per option and act
/// (<see cref="RunPenaltyState.RestSiteTabsThisAct"/>).
/// </summary>
public static class RestSiteTabs
{
    /// <summary>The options, in display order.</summary>
    public static IReadOnlyList<RestSiteTab> All { get; } = [RestSiteTab.Open, RestSiteTab.RunUp];

    /// <summary>Whether rest sites show the options at all under these rules.</summary>
    public static bool IsOffered(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Enabled && rules.RestSiteTabs != RestSiteTabLimit.Off;
    }

    /// <summary>Whether <paramref name="player"/> can still pick <paramref name="tab"/> this act.</summary>
    public static bool IsAvailable(RuleSet rules, RunPenaltyState state, PlayerId player, RestSiteTab tab)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsOffered(rules)
            && (rules.RestSiteTabs == RestSiteTabLimit.Unlimited || state.RestSiteTabCount(player, tab) == 0);
    }
}
