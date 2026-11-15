using SlayTheSpirits.Rules;
using SpireKit.Determinism;

namespace SlayTheSpirits.Engine;

/// <summary>A point on the act map, by column and row.</summary>
public readonly record struct MapSpot(int Col, int Row);

/// <summary>
/// Which shops are the Tavern (<see cref="RuleSet.Taverns"/>). The pick only depends on the run's seed, the act and
/// where the act's shops are, so every peer agrees, the map can mark it before anyone gets there, and reloading a
/// save picks the same shop.
/// </summary>
public static class Taverns
{
    /// <summary>Whether any shop can be the Tavern under these rules.</summary>
    public static bool IsOffered(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Enabled && rules.Taverns != TavernLimit.Off;
    }

    /// <summary>Whether the shop at <paramref name="spot"/> is the Tavern.</summary>
    /// <param name="rules">The rules in effect.</param>
    /// <param name="runSeed">The run's seed (the same string on every peer).</param>
    /// <param name="actIndex">0-based act.</param>
    /// <param name="shops">Every shop on this act's map.</param>
    /// <param name="spot">The shop being asked about.</param>
    public static bool IsTavern(RuleSet rules, string runSeed, int actIndex, IReadOnlyCollection<MapSpot> shops, MapSpot spot)
    {
        ArgumentNullException.ThrowIfNull(shops);
        if (!IsOffered(rules) || !shops.Contains(spot))
        {
            return false;
        }

        return rules.Taverns == TavernLimit.EveryShop || Pick(runSeed, actIndex, shops) == spot;
    }

    /// <summary>The act's Tavern under <see cref="TavernLimit.OncePerAct"/>.</summary>
    internal static MapSpot Pick(string runSeed, int actIndex, IReadOnlyCollection<MapSpot> shops)
    {
        // Sorted so the answer doesn't depend on the order the game lists its map points in.
        var ordered = shops.Distinct().OrderBy(static s => s.Row).ThenBy(static s => s.Col).ToList();
        var hash = StableHash.Combine(StableHash.Fnv1a64(runSeed), StableHash.Fnv1a64($"tavern.act{actIndex}"));
        return ordered[(int)(hash % (ulong)ordered.Count)];
    }
}
