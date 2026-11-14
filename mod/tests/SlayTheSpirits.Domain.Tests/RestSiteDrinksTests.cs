using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SpireKit.Storage;
using static SlayTheSpirits.Tests.TestParty;

namespace SlayTheSpirits.Tests;

public sealed class RestSiteDrinksTests
{
    private static readonly PenaltyCause RestSite = new("restsite.shot", "Rest site");

    private static PenaltyEngine Engine(RuleSet rules) => new(rules, new RunPenaltyState(), 1);

    [Fact]
    public void Standard_offers_each_drink_once_per_act_and_they_come_back_next_act()
    {
        var engine = Engine(Presets.Standard);

        engine.TakeRestSiteDrink(Alice, PenaltyKind.Shot, RestSite, Four);

        Assert.False(RestSiteDrinks.IsAvailable(Presets.Standard, engine.State, Alice, PenaltyKind.Shot));
        Assert.True(RestSiteDrinks.IsAvailable(Presets.Standard, engine.State, Alice, PenaltyKind.Chug));
        Assert.True(RestSiteDrinks.IsAvailable(Presets.Standard, engine.State, Bob, PenaltyKind.Shot));
        engine.EnterAct(2);
        Assert.True(RestSiteDrinks.IsAvailable(Presets.Standard, engine.State, Alice, PenaltyKind.Shot));
    }

    [Fact]
    public void Other_drinks_never_use_up_the_rest_site_option()
    {
        var engine = Engine(Presets.Standard);

        engine.Handle(new PenaltyRequest(new Penalty(PenaltyKind.Shot), TargetKind.Subject, Alice, Alice, new("player.died")), Four);

        Assert.True(RestSiteDrinks.IsAvailable(Presets.Standard, engine.State, Alice, PenaltyKind.Shot));
    }

    [Fact]
    public void Rest_site_drinks_are_never_downgraded_but_count_toward_caps()
    {
        var engine = Engine(Presets.Standard); // Shot cap 1.
        engine.Handle(new PenaltyRequest(new Penalty(PenaltyKind.Shot), TargetKind.Subject, Alice, Alice, new("player.died")), Four);

        var drink = Assert.Single(engine.TakeRestSiteDrink(Alice, PenaltyKind.Shot, RestSite, Four));

        Assert.Equal(new Penalty(PenaltyKind.Shot), drink.Penalty);
        Assert.Empty(drink.Notes);
        Assert.Equal(2, engine.State.CountThisAct(Alice, PenaltyKind.Shot));
    }

    [Fact]
    public void Voluntary_drinks_skip_redirects_and_immunity()
    {
        var engine = Engine(Presets.Standard);
        engine.AddRedirects(Alice);
        engine.GrantImmunity(Alice);

        var drink = Assert.Single(engine.Handle(
            new PenaltyRequest(new Penalty(PenaltyKind.Chug), TargetKind.Subject, Alice, Alice, new("POTION.SPIRITS_TALLBOY"), Voluntary: true),
            Four));

        Assert.Equal(Alice, drink.Player);
        Assert.Equal(1, engine.State.PendingRedirects[Alice.Value]); // Still waiting for a drink they didn't choose.
    }

    [Fact]
    public void Unlimited_offers_them_at_every_rest_site()
    {
        var engine = Engine(Presets.Degenerate);

        engine.TakeRestSiteDrink(Alice, PenaltyKind.Shot, RestSite, Four);
        engine.TakeRestSiteDrink(Alice, PenaltyKind.Shot, RestSite, Four);

        Assert.True(RestSiteDrinks.IsAvailable(Presets.Degenerate, engine.State, Alice, PenaltyKind.Shot));
    }

    [Fact]
    public void Casual_switched_off_and_disabled_rules_offer_nothing()
    {
        Assert.False(RestSiteDrinks.IsOffered(Presets.Casual));
        Assert.False(RestSiteDrinks.IsOffered(Presets.Standard with { RestSiteDrinks = RestSiteDrinkLimit.Off }));
        Assert.False(RestSiteDrinks.IsOffered(Presets.Standard with { Enabled = false }));
        Assert.False(RestSiteDrinks.IsAvailable(Presets.Casual, new RunPenaltyState(), Alice, PenaltyKind.Chug));
    }

    [Fact]
    public void Rest_site_drinks_this_act_survive_a_save()
    {
        var engine = Engine(Presets.Standard);
        engine.TakeRestSiteDrink(Alice, PenaltyKind.Chug, RestSite, Four);

        var restored = RunPenaltyStateCodec.FromJson(RunPenaltyStateCodec.ToJson(engine.State))!;

        Assert.Equal(1, restored.RestSiteDrinkCount(Alice, PenaltyKind.Chug));
    }

    [Theory]
    [InlineData("true", RestSiteDrinkLimit.OncePerAct)]
    [InlineData("false", RestSiteDrinkLimit.Off)]
    public void Version_2_on_off_switch_becomes_a_limit(string value, RestSiteDrinkLimit expected)
    {
        var v2 = $$$"""{"schema":2,"data":{"name":"Mine","restSiteDrinks":{{{value}}}}}""";

        var result = RuleSetCodec.FromJson(v2);

        Assert.Equal(DecodeStatus.Migrated, result.Status);
        Assert.Equal(expected, result.Value.RestSiteDrinks);
    }
}
