using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Engine;

/// <summary>One thing on the Tavern's menu.</summary>
/// <param name="Id">Stable id, e.g. <c>house_lager</c> (art file names, drink causes).</param>
/// <param name="Name">Shown on the menu.</param>
/// <param name="Description">What it does.</param>
/// <param name="Gold">Base gold price; see <see cref="TavernMenu.GoldFor"/>.</param>
/// <param name="Drink">What the buyer drinks for it, if anything.</param>
/// <param name="GoldPerOtherPlayer"><see cref="Gold"/> is per teammate (a round for the table).</param>
public sealed record TavernItem(string Id, string Name, string Description, int Gold, PenaltyKind? Drink, bool GoldPerOtherPlayer = false);

/// <summary>
/// The Tavern's menu: prices in gold and drinks. Each item can be bought once per visit. Drinks bought here are
/// voluntary (<see cref="PenaltyRequest.Voluntary"/>): the price on the menu is the price you pay.
/// </summary>
public static class TavernMenu
{
    public const int LagerHeal = 12;
    public const int WaterHeal = 5;
    public const int RoundHeal = 6;
    public const int SpecialUpgrades = 2;

    public static TavernItem HouseLager { get; } = new(
        "house_lager", "House Lager", $"Heal {LagerHeal} HP.", 25, PenaltyKind.Sip);

    public static TavernItem TheSpecial { get; } = new(
        "the_special", "The Special", $"Upgrade {SpecialUpgrades} random cards.", 60, PenaltyKind.Finish);

    public static TavernItem TopShelf { get; } = new(
        "top_shelf", "Top Shelf", "Choose a Tavern relic.", 100, PenaltyKind.Shot);

    public static TavernItem GlassOfWater { get; } = new(
        "glass_of_water", "Glass of Water", $"Heal {WaterHeal} HP. On the house.", 0, PenaltyKind.Water);

    public static TavernItem BuyARound { get; } = new(
        "buy_a_round", "Buy a Round", $"Everyone else takes a Sip. Everyone heals {RoundHeal} HP.", 15, null, GoldPerOtherPlayer: true);

    public static TavernItem TheBouncer { get; } = new(
        "the_bouncer", "The Bouncer", "Remove a card from your deck.", 75, null);

    /// <summary>Pay off a rest site tab: Finish your drink to remove a Tavern curse.</summary>
    public static TavernItem SettleUp { get; } = new(
        "settle_up", "Settle Up", "Remove a Tavern curse from your deck.", 0, PenaltyKind.Finish);

    /// <summary>The menu, in display order.</summary>
    public static IReadOnlyList<TavernItem> Items { get; } = [HouseLager, TheSpecial, TopShelf, GlassOfWater, BuyARound, TheBouncer, SettleUp];

    /// <summary>The gold price for a party of <paramref name="players"/> (before the game's shop discounts).</summary>
    public static int GoldFor(TavernItem item, int players)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.GoldPerOtherPlayer ? item.Gold * Math.Max(1, players - 1) : item.Gold;
    }
}
