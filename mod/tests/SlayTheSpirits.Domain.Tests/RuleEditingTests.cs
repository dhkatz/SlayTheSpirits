using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;

namespace SlayTheSpirits.Tests;

public sealed class RuleEditingTests
{
    [Fact]
    public void Each_preset_is_recognized()
    {
        for (var i = 0; i < Presets.All.Count; i++)
        {
            Assert.Equal(i, RuleEditing.PresetIndex(Presets.All[i]));
        }
    }

    [Fact]
    public void Editing_a_preset_makes_it_custom_and_undoing_restores_the_name()
    {
        var boss = BuiltInTriggers.BossDefeated;
        var original = RuleEditing.RuleFor(Presets.Standard, boss);

        var edited = RuleEditing.WithTrigger(Presets.Standard, boss, original with { Enabled = false });
        Assert.Equal(RuleEditing.CustomName, edited.Name);
        Assert.Equal(-1, RuleEditing.PresetIndex(edited));

        var undone = RuleEditing.WithTrigger(edited, boss, original);
        Assert.Equal("Standard", undone.Name);
    }

    [Fact]
    public void A_users_own_name_is_kept_through_edits()
    {
        var mine = RuleEditing.WithTrigger(Presets.Standard, BuiltInTriggers.RunWon, TriggerRule.On(PenaltyKind.Chug, TargetKind.Everyone)) with { Name = "Dave's House Rules" };

        var edited = RuleEditing.WithTrigger(mine, BuiltInTriggers.RunLost, TriggerRule.On(PenaltyKind.Shot, TargetKind.Everyone));

        Assert.Equal("Dave's House Rules", edited.Name);
    }

    [Fact]
    public void Missing_triggers_default_to_the_standard_rule_switched_off()
    {
        var empty = new RuleSet();

        var rule = RuleEditing.RuleFor(empty, BuiltInTriggers.PlayerBigHit);

        Assert.False(rule.Enabled);
        Assert.Equal(Presets.Standard.Triggers[BuiltInTriggers.PlayerBigHit.Id.Value] with { Enabled = false }, rule);
    }

    public static TheoryData<string> EditableTriggerIds => [.. RuleEditing.EditableTriggers.Select(t => t.Id.Value)];

    [Theory]
    [MemberData(nameof(EditableTriggerIds))]
    public void Every_offered_choice_passes_validation(string id)
    {
        var trigger = BuiltInTriggers.Find(id)!;
        Assert.NotEqual(id, RuleEditing.TextFor(trigger).Name); // Has a real display name.
        foreach (var target in RuleEditing.TargetsFor(trigger))
        {
            foreach (var kind in Enum.GetValues<PenaltyKind>())
            {
                var rule = RuleEditing.RuleFor(Presets.Standard, trigger) with { Enabled = true, Target = target, Penalty = new Penalty(kind, Penalty.MaxCount) };

                var rules = RuleEditing.WithTrigger(Presets.Standard, trigger, rule);

                Assert.Empty(RuleSetValidator.Validate(rules));
            }
        }
    }

    [Fact]
    public void Labels_use_title_case_like_the_games_settings()
    {
        string[] minor = ["a", "an", "the", "at", "of", "in", "on", "to", "and", "or", "for"];
        var labels = RuleEditing.EditableTriggers
            .Select(RuleEditing.TextFor)
            .SelectMany(t => new[] { t.Name, t.ThresholdLabel })
            .Concat(Enum.GetValues<TargetKind>().Select(RuleEditing.TargetName))
            .OfType<string>();

        foreach (var label in labels)
        {
            var words = label.Split(' ');
            Assert.All(
                words.Select((word, i) => (word, i)).Where(w => w.i == 0 || !minor.Contains(w.word)),
                w => Assert.True(char.IsUpper(w.word[0]), $"'{w.word}' in '{label}' should be capitalized."));
        }
    }

    [Fact]
    public void Threshold_triggers_say_what_the_threshold_means()
    {
        foreach (var trigger in RuleEditing.EditableTriggers.Where(t => t.UsesThreshold))
        {
            var text = RuleEditing.TextFor(trigger);
            Assert.NotNull(text.ThresholdLabel);
            Assert.True(text.ThresholdMax > 0);
        }
    }
}
