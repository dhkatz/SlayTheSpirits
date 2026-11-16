using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SpireKit.Storage;
using static SlayTheSpirits.Tests.TestParty;

namespace SlayTheSpirits.Tests;

public sealed class RestSiteTabsTests
{
    private static PenaltyEngine Engine(RuleSet rules) => new(rules, new RunPenaltyState(), 1);

    [Fact]
    public void Standard_offers_each_tab_once_per_act_and_they_come_back_next_act()
    {
        var engine = Engine(Presets.Standard);

        engine.TakeRestSiteTab(Alice, RestSiteTab.RunUp);

        Assert.False(RestSiteTabs.IsAvailable(Presets.Standard, engine.State, Alice, RestSiteTab.RunUp));
        Assert.True(RestSiteTabs.IsAvailable(Presets.Standard, engine.State, Alice, RestSiteTab.Open));
        Assert.True(RestSiteTabs.IsAvailable(Presets.Standard, engine.State, Bob, RestSiteTab.RunUp));
        engine.EnterAct(2);
        Assert.True(RestSiteTabs.IsAvailable(Presets.Standard, engine.State, Alice, RestSiteTab.RunUp));
    }

    [Fact]
    public void A_tab_costs_no_drink()
    {
        var engine = Engine(Presets.Standard);

        engine.TakeRestSiteTab(Alice, RestSiteTab.RunUp);

        Assert.Equal(0, engine.TipsyOf(Alice));
    }

    [Fact]
    public void Voluntary_drinks_skip_redirects_and_immunity()
    {
        var engine = Engine(Presets.Standard);
        engine.AddRedirects(Alice);
        engine.GrantImmunity(Alice);

        var drink = Assert.Single(engine.Handle(
            new PenaltyRequest(new Penalty(PenaltyKind.Sip, 2), TargetKind.Subject, Alice, Alice, new("POTION.SPIRITS_TALLBOY"), Voluntary: true),
            Four));

        Assert.Equal(Alice, drink.Player);
        Assert.Equal(1, engine.State.PendingRedirects[Alice.Value]); // Still waiting for a drink they didn't choose.
    }

    [Fact]
    public void Unlimited_offers_them_at_every_rest_site()
    {
        var engine = Engine(Presets.Degenerate);

        engine.TakeRestSiteTab(Alice, RestSiteTab.RunUp);
        engine.TakeRestSiteTab(Alice, RestSiteTab.RunUp);

        Assert.True(RestSiteTabs.IsAvailable(Presets.Degenerate, engine.State, Alice, RestSiteTab.RunUp));
    }

    [Fact]
    public void Casual_switched_off_and_disabled_rules_offer_nothing()
    {
        Assert.False(RestSiteTabs.IsOffered(Presets.Casual));
        Assert.False(RestSiteTabs.IsOffered(Presets.Standard with { RestSiteTabs = RestSiteTabLimit.Off }));
        Assert.False(RestSiteTabs.IsOffered(Presets.Standard with { Enabled = false }));
        Assert.False(RestSiteTabs.IsAvailable(Presets.Casual, new RunPenaltyState(), Alice, RestSiteTab.Open));
    }

    [Fact]
    public void Tabs_this_act_survive_a_save()
    {
        var engine = Engine(Presets.Standard);
        engine.TakeRestSiteTab(Alice, RestSiteTab.Open);

        var restored = RunPenaltyStateCodec.FromJson(RunPenaltyStateCodec.ToJson(engine.State))!;

        Assert.Equal(1, restored.RestSiteTabCount(Alice, RestSiteTab.Open));
    }

    [Fact]
    public void Runs_saved_with_rest_site_drinks_count_them_as_tabs()
    {
        const string v2 = """
            {"schema":2,"data":{"act":2,"restSiteDrinksThisAct":{"101":{"Chug":1},"202":{"Shot":1,"Chug":1}}}}
            """;

        var state = RunPenaltyStateCodec.FromJson(v2);

        Assert.NotNull(state);
        Assert.Equal(1, state.RestSiteTabCount(Alice, RestSiteTab.Open));
        Assert.Equal(0, state.RestSiteTabCount(Alice, RestSiteTab.RunUp));
        Assert.Equal(1, state.RestSiteTabCount(Bob, RestSiteTab.RunUp));
        Assert.Equal(1, state.RestSiteTabCount(Bob, RestSiteTab.Open));
    }

    [Fact]
    public void The_setting_keeps_its_saved_name()
    {
        var json = RuleSetCodec.ToJson(Presets.Degenerate);

        Assert.Contains("\"restSiteDrinks\": \"Unlimited\"", json, StringComparison.Ordinal);
        Assert.Equal(RestSiteTabLimit.Unlimited, RuleSetCodec.FromJson(json).Value.RestSiteTabs);
    }

    [Theory]
    [InlineData("true", RestSiteTabLimit.OncePerAct)]
    [InlineData("false", RestSiteTabLimit.Off)]
    public void Version_2_on_off_switch_becomes_a_limit(string value, RestSiteTabLimit expected)
    {
        var v2 = $$$"""{"schema":2,"data":{"name":"Mine","restSiteDrinks":{{{value}}}}}""";

        var result = RuleSetCodec.FromJson(v2);

        Assert.Equal(DecodeStatus.Migrated, result.Status);
        Assert.Equal(expected, result.Value.RestSiteTabs);
    }
}
