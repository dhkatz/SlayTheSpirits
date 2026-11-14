using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using SpireKit.Sharing;
using SpireKit.Storage;

namespace SlayTheSpirits.Tests;

/// <summary>Schema v1 files used a "Drink" step (Water, Sip, Drink, Shot, Chug, Finish); v2 dropped it.</summary>
public sealed class LadderMigrationTests
{
    private const string OldDefaultTipsy = """{"Water":0,"Sip":1,"Drink":2,"Shot":3,"Chug":4,"Finish":5}""";

    [Fact]
    public void Old_presets_become_the_new_presets()
    {
        var v1 = $$$"""
            {"schema":1,"data":{"name":"Standard","enabled":true,
              "triggers":{"encounter.boss.start":{"enabled":true,"penalty":{"kind":"Shot","count":1},"target":"Everyone"}},
              "capsPerAct":{"Shot":3,"Chug":1},"tipsy":{{{OldDefaultTipsy}}}}}
            """;

        var result = RuleSetCodec.FromJson(v1);

        Assert.Equal(DecodeStatus.Migrated, result.Status);
        Assert.Equal(Presets.All.ToList().IndexOf(Presets.Standard), RuleEditing.PresetIndex(result.Value));
    }

    [Fact]
    public void Custom_rules_turn_drinks_into_sips_and_take_the_new_tipsy_scale()
    {
        var v1 = $$$"""
            {"schema":1,"data":{"name":"Dave's House Rules",
              "triggers":{"player.died":{"enabled":true,"penalty":{"kind":"Drink","count":2},"target":"Subject"}},
              "enemies":{"MONSTER.GREMLIN_NOB":{"enabled":true,"penalty":{"kind":"Drink","count":1},"target":"Everyone"}},
              "capsPerAct":{"Drink":4,"Shot":1},"labels":{"Drink":{"singular":"Gulp","plural":"Gulps"}},
              "tipsy":{{{OldDefaultTipsy}}}}}
            """;

        var rules = RuleSetCodec.FromJson(v1).Value;

        Assert.Equal("Dave's House Rules", rules.Name);
        Assert.Equal(new Penalty(PenaltyKind.Sip, 2), rules.Triggers[BuiltInTriggers.PlayerDied.Id.Value].Penalty);
        Assert.Equal(PenaltyKind.Sip, rules.Enemies["MONSTER.GREMLIN_NOB"].Penalty.Kind);
        Assert.Equal(new Dictionary<PenaltyKind, int> { [PenaltyKind.Shot] = 1 }, rules.CapsPerAct);
        Assert.Empty(rules.Labels);
        Assert.Equal(TipsyWeights.Defaults, rules.Tipsy);
    }

    [Fact]
    public void Hand_tuned_tipsy_weights_are_kept()
    {
        const string v1 = """{"schema":1,"data":{"name":"Mine","tipsy":{"Sip":2,"Drink":3,"Shot":9}}}""";

        var rules = RuleSetCodec.FromJson(v1).Value;

        Assert.Equal(2, rules.Tipsy[PenaltyKind.Sip]);
        Assert.Equal(9, rules.Tipsy[PenaltyKind.Shot]);
        Assert.Equal(2, rules.Tipsy.Count);
    }

    [Fact]
    public void Old_share_codes_still_import()
    {
        const string v1 = """{"schema":1,"data":{"name":"Shared","triggers":{"run.lost":{"enabled":true,"penalty":{"kind":"Drink","count":1},"target":"Everyone"}}}}""";
        var code = ShareCode.Encode(RuleSetCodec.ShareCodePrefix, v1);

        Assert.True(RuleSetCodec.TryFromShareCode(code, out var rules, out _));
        Assert.Equal(PenaltyKind.Sip, rules.Triggers[BuiltInTriggers.RunLost.Id.Value].Penalty.Kind);
    }

    [Fact]
    public void Saved_runs_count_old_drinks_as_sips()
    {
        const string v1 = """
            {"schema":1,"data":{"act":2,"rngDraws":5,
              "actCounts":{"7":{"Sip":1,"Drink":2}},
              "tally":{"totals":{"7":{"Drink":3,"Shot":1}},"tipsy":{"7":9},
                "worstMoments":{"7":{"penalty":{"kind":"Drink","count":1},"sourceId":"player.died","label":null}}}}}
            """;

        var state = RunPenaltyStateCodec.FromJson(v1);

        Assert.NotNull(state);
        Assert.Equal(3, state.ActCounts[7][PenaltyKind.Sip]);
        Assert.Equal(3, state.Tally.Totals[7][PenaltyKind.Sip]);
        Assert.Equal(1, state.Tally.Totals[7][PenaltyKind.Shot]);
        Assert.Equal(PenaltyKind.Sip, state.Tally.WorstMoments[7].Penalty.Kind);
        Assert.Equal(9, state.Tally.Tipsy[7]);
    }
}
