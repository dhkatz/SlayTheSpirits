using SlayTheSpirits.Engine;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Domain.Tests;

public sealed class TavernTests
{
    private static readonly MapSpot[] Shops = [new(1, 4), new(3, 4), new(2, 9), new(5, 11)];

    private static int TavernCount(RuleSet rules, string seed, int act, IReadOnlyCollection<MapSpot> shops) =>
        shops.Count(s => Taverns.IsTavern(rules, seed, act, shops, s));

    [Fact]
    public void Once_per_act_picks_exactly_one_shop()
    {
        foreach (var seed in new[] { "ABC123", "ZZZZ", "7" })
        {
            for (var act = 0; act < 3; act++)
            {
                Assert.Equal(1, TavernCount(Presets.Standard, seed, act, Shops));
            }
        }
    }

    [Fact]
    public void The_pick_ignores_the_order_shops_are_listed_in()
    {
        var reversed = Shops.Reverse().ToArray();
        for (var act = 0; act < 3; act++)
        {
            Assert.Equal(Taverns.Pick("SEED", act, Shops), Taverns.Pick("SEED", act, reversed));
        }
    }

    [Fact]
    public void Different_seeds_pick_different_shops()
    {
        var picks = Enumerable.Range(0, 40).Select(i => Taverns.Pick($"seed{i}", 0, Shops)).Distinct().Count();
        Assert.True(picks > 1);
    }

    [Fact]
    public void Every_shop_makes_them_all_taverns()
    {
        Assert.Equal(Shops.Length, TavernCount(Presets.Standard with { Taverns = TavernLimit.EveryShop }, "SEED", 0, Shops));
    }

    [Fact]
    public void Off_disabled_rules_and_non_shops_are_never_taverns()
    {
        Assert.Equal(0, TavernCount(Presets.Standard with { Taverns = TavernLimit.Off }, "SEED", 0, Shops));
        Assert.Equal(0, TavernCount(Presets.Standard with { Enabled = false }, "SEED", 0, Shops));
        Assert.False(Taverns.IsTavern(Presets.Standard with { Taverns = TavernLimit.EveryShop }, "SEED", 0, Shops, new MapSpot(0, 0)));
        Assert.False(Taverns.IsTavern(Presets.Standard, "SEED", 0, [], new MapSpot(1, 4)));
    }

    [Fact]
    public void Blitz_gets_its_tavern_after_the_treasure()
    {
        Assert.Equal(5, Taverns.BlitzTavernIndex(["Monster", "Monster", "Shop", "Elite", "Treasure", "RestSite"], "Treasure"));
        Assert.Equal(3, Taverns.BlitzTavernIndex(["Monster", "Shop", "Elite"], "Treasure"));
    }

    [Fact]
    public void Presets_scale_with_how_much_drinking_they_want()
    {
        Assert.False(Taverns.IsOffered(Presets.Casual));
        Assert.Equal(TavernLimit.OncePerAct, Presets.Standard.Taverns);
        Assert.Equal(TavernLimit.EveryShop, Presets.Degenerate.Taverns);
    }

    [Fact]
    public void Rule_files_without_the_setting_get_the_default()
    {
        var json = RuleSetCodec.ToJson(Presets.Standard).Replace("\"taverns\": \"OncePerAct\",", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("taverns", json, StringComparison.Ordinal);
        Assert.Equal(TavernLimit.OncePerAct, RuleSetCodec.FromJson(json).Value.Taverns);
    }

    [Fact]
    public void A_round_costs_per_teammate()
    {
        Assert.Equal(15, TavernMenu.GoldFor(TavernMenu.BuyARound, 2));
        Assert.Equal(45, TavernMenu.GoldFor(TavernMenu.BuyARound, 4));
        Assert.Equal(25, TavernMenu.GoldFor(TavernMenu.HouseLager, 4));
    }

    [Fact]
    public void Menu_ids_are_unique()
    {
        Assert.Equal(TavernMenu.Items.Count, TavernMenu.Items.Select(static i => i.Id).Distinct().Count());
    }
}
