using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SpireKit.Sharing;
using SpireKit.Storage;

namespace SlayTheSpirits.Tests;

/// <summary>Schema v3 defaulted Tipsy to 1 / 3 / 5 / 10 (Sip / Finish / Chug / Shot); v4 uses 1 / 6 / 8 / 10.</summary>
public sealed class TipsyWeightsMigrationTests
{
    private const string OldDefaults = """{"Water":0,"Sip":1,"Finish":3,"Chug":5,"Shot":10}""";

    [Fact]
    public void Profiles_on_the_old_defaults_take_the_new_ones()
    {
        var v3 = $$$"""{"schema":3,"data":{"name":"Friday","restSiteDrinks":"OncePerAct","tipsy":{{{OldDefaults}}}}}""";

        var result = RuleSetCodec.FromJson(v3);

        Assert.Equal(DecodeStatus.Migrated, result.Status);
        Assert.Equal("Friday", result.Value.Name);
        Assert.Equal(TipsyWeights.Defaults, result.Value.Tipsy);
        Assert.Equal(6, result.Value.Tipsy[PenaltyKind.Finish]);
        Assert.Equal(8, result.Value.Tipsy[PenaltyKind.Chug]);
    }

    [Fact]
    public void Hand_tuned_weights_are_kept()
    {
        const string v3 = """{"schema":3,"data":{"name":"Mine","tipsy":{"Water":0,"Sip":2,"Finish":3,"Chug":5,"Shot":10}}}""";

        var tipsy = RuleSetCodec.FromJson(v3).Value.Tipsy;

        Assert.Equal(2, tipsy[PenaltyKind.Sip]);
        Assert.Equal(3, tipsy[PenaltyKind.Finish]);
        Assert.Equal(5, tipsy[PenaltyKind.Chug]);
    }

    [Fact]
    public void Old_share_codes_take_the_new_defaults()
    {
        var v3 = $$$"""{"schema":3,"data":{"name":"Shared","tipsy":{{{OldDefaults}}}}}""";

        Assert.True(RuleSetCodec.TryFromShareCode(ShareCode.Encode(RuleSetCodec.ShareCodePrefix, v3), out var rules, out _));
        Assert.Equal(TipsyWeights.Defaults, rules.Tipsy);
    }

    [Fact]
    public void Current_profiles_round_trip_unchanged()
    {
        var rules = Presets.Standard with { Tipsy = new Dictionary<PenaltyKind, int>(TipsyWeights.Defaults) { [PenaltyKind.Shot] = 12 } };

        var decoded = RuleSetCodec.FromJson(RuleSetCodec.ToJson(rules));

        Assert.Equal(DecodeStatus.Ok, decoded.Status);
        Assert.Equal(12, decoded.Value.Tipsy[PenaltyKind.Shot]);
    }
}
