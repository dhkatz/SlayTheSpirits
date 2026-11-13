using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using static SlayTheSpirits.Tests.TestParty;

namespace SlayTheSpirits.Tests;

public sealed class RuleResolverTests
{
    [Fact]
    public void Boss_start_maps_to_everyone_shot_in_standard()
    {
        var request = RuleResolver.Resolve(Presets.Standard, new TriggerOccurrence(BuiltInTriggers.BossEncounterStarted.Id, Label: "The Guardian"));

        Assert.NotNull(request);
        Assert.Equal(Penalty.Of(PenaltyKind.Shot), request.Penalty);
        Assert.Equal(TargetKind.Everyone, request.Target);
        Assert.Equal("The Guardian", request.Cause.Label);
    }

    [Fact]
    public void Disabled_rules_and_master_switch_produce_nothing()
    {
        var potion = new TriggerOccurrence(BuiltInTriggers.TurnEndedWithEnergy.Id, Alice, Magnitude: 3);
        Assert.Null(RuleResolver.Resolve(Presets.Standard, potion));

        var boss = new TriggerOccurrence(BuiltInTriggers.BossEncounterStarted.Id);
        Assert.Null(RuleResolver.Resolve(Presets.Standard with { Enabled = false }, boss));
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(45, true)]
    public void Threshold_rules_fire_only_at_or_above_threshold(int damage, bool fires)
    {
        var hit = new TriggerOccurrence(BuiltInTriggers.PlayerBigHit.Id, Alice, Magnitude: damage);

        Assert.Equal(fires, RuleResolver.Resolve(Presets.Standard, hit) is not null);
    }

    [Fact]
    public void Enemy_rules_are_keyed_by_monster_id()
    {
        var rules = Presets.Standard with
        {
            Enemies = new Dictionary<string, TriggerRule> { ["MONSTER.GREMLIN_NOB"] = TriggerRule.On(PenaltyKind.Drink, TargetKind.Everyone) },
        };

        Assert.NotNull(RuleResolver.Resolve(rules, new TriggerOccurrence(BuiltInTriggers.EnemyEncountered.Id, Detail: "MONSTER.GREMLIN_NOB")));
        Assert.Null(RuleResolver.Resolve(rules, new TriggerOccurrence(BuiltInTriggers.EnemyEncountered.Id, Detail: "MONSTER.CULTIST")));
        Assert.Null(RuleResolver.Resolve(rules, new TriggerOccurrence(BuiltInTriggers.EnemyEncountered.Id)));
    }

    [Fact]
    public void Unknown_triggers_are_ignored()
    {
        Assert.Null(RuleResolver.Resolve(Presets.Standard, new TriggerOccurrence(new TriggerId("future.trigger"))));
    }
}
