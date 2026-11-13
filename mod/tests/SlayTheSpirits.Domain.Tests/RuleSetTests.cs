using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using SpireKit.Storage;

namespace SlayTheSpirits.Tests;

public sealed class RuleSetTests
{
    public static TheoryData<string> PresetNames => [.. Presets.All.Select(p => p.Name)];

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Presets_are_valid(string name)
    {
        var preset = Presets.All.Single(p => p.Name == name);

        Assert.Empty(RuleSetValidator.Validate(preset));
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void Presets_survive_json_and_share_code_round_trips(string name)
    {
        var preset = Presets.All.Single(p => p.Name == name);

        var fromJson = RuleSetCodec.FromJson(RuleSetCodec.ToJson(preset));
        Assert.Equal(DecodeStatus.Ok, fromJson.Status);
        AssertEquivalent(preset, fromJson.Value);

        Assert.True(RuleSetCodec.TryFromShareCode(RuleSetCodec.ToShareCode(preset), out var fromCode, out var error), error);
        AssertEquivalent(preset, fromCode);
    }

    [Fact]
    public void Share_codes_are_short_enough_to_paste()
    {
        Assert.InRange(RuleSetCodec.ToShareCode(Presets.Standard).Length, 1, 1500);
    }

    [Fact]
    public void Json_uses_readable_names()
    {
        var json = RuleSetCodec.ToJson(Presets.Standard);

        Assert.Contains("\"schema\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"encounter.boss.start\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Shot\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Everyone\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_rejects_subject_targets_on_subjectless_triggers()
    {
        var rules = Presets.Standard with
        {
            Triggers = new Dictionary<string, TriggerRule>(Presets.Standard.Triggers)
            {
                [BuiltInTriggers.BossEncounterStarted.Id.Value] = TriggerRule.On(PenaltyKind.Shot, TargetKind.Subject),
            },
        };

        Assert.Contains(RuleSetValidator.Validate(rules), p => p.Contains("no subject", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_share_codes_are_rejected_with_a_reason()
    {
        Assert.False(RuleSetCodec.TryFromShareCode("hello", out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Labels_fall_back_to_defaults()
    {
        Assert.Equal("3 Shots", PenaltyLabels.Describe(Presets.Standard, new Penalty(PenaltyKind.Shot, 3)));
        Assert.Equal("Shot of pickle juice", PenaltyLabels.Describe(Presets.Sober, Penalty.Of(PenaltyKind.Shot)));
        Assert.Equal("Water", PenaltyLabels.Describe(Presets.Sober, Penalty.Of(PenaltyKind.Water)));
    }

    private static void AssertEquivalent(RuleSet expected, RuleSet actual) =>
        Assert.Equal(RuleSetCodec.ToJson(expected), RuleSetCodec.ToJson(actual));
}
