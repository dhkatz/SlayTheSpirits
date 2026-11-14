using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Engine;

/// <summary>
/// The voluntary heavy drinks at rest sites: a <see cref="PenaltyKind.Chug"/> for a pick of cards and a
/// <see cref="PenaltyKind.Shot"/> for a relic. How often they're offered is <see cref="RuleSet.RestSiteDrinks"/>;
/// they're counted on their own (<see cref="RunPenaltyState.RestSiteDrinksThisAct"/>), so a Shot from a death or a
/// card never uses up the rest site's.
/// </summary>
public static class RestSiteDrinks
{
    /// <summary>The drinks rest sites can offer, in display order.</summary>
    public static IReadOnlyList<PenaltyKind> Kinds { get; } = [PenaltyKind.Chug, PenaltyKind.Shot];

    /// <summary>Whether rest sites show the options at all under these rules.</summary>
    public static bool IsOffered(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Enabled && rules.RestSiteDrinks != RestSiteDrinkLimit.Off;
    }

    /// <summary>Whether <paramref name="player"/> can still pick <paramref name="kind"/> this act.</summary>
    public static bool IsAvailable(RuleSet rules, RunPenaltyState state, PlayerId player, PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsOffered(rules)
            && (rules.RestSiteDrinks == RestSiteDrinkLimit.Unlimited || state.RestSiteDrinkCount(player, kind) == 0);
    }
}
