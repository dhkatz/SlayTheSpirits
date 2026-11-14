using SlayTheSpirits.Penalties;
using SlayTheSpirits.Triggers;

namespace SlayTheSpirits.Rules;

/// <summary>How a trigger is presented in the rules editor.</summary>
/// <param name="Name">Row label, e.g. "Boss Defeated".</param>
/// <param name="ThresholdLabel">For threshold triggers, the label of the threshold row.</param>
/// <param name="ThresholdUnit">Unit after the threshold value, e.g. "damage".</param>
/// <param name="ThresholdMax">Largest threshold offered.</param>
public sealed record TriggerText(string Name, string? ThresholdLabel = null, string? ThresholdUnit = null, int ThresholdMax = 0);

/// <summary>
/// Pure editing operations behind the rules editor, so the UI only wires controls to them. Every edit goes through
/// <see cref="Named"/>, which keeps the name honest: a rule set equal to a preset carries that preset's name, an
/// edited preset becomes "Custom", and a user's own name (e.g. from a share code) is kept.
/// </summary>
public static class RuleEditing
{
    public const string CustomName = "Custom";

    /// <summary>Triggers shown in the editor, in display order (per-enemy rules are edited elsewhere).</summary>
    public static IReadOnlyList<TriggerDefinition> EditableTriggers { get; } =
        [.. BuiltInTriggers.All.Where(static t => !t.IsKeyedByDetail)];

    private static readonly Dictionary<string, TriggerText> Texts = new(StringComparer.Ordinal)
    {
        [BuiltInTriggers.BossEncounterStarted.Id.Value] = new("Boss Fight"),
        [BuiltInTriggers.BossDefeated.Id.Value] = new("Boss Defeated"),
        [BuiltInTriggers.EliteEncounterStarted.Id.Value] = new("Elite Fight"),
        [BuiltInTriggers.EnemyEncountered.Id.Value] = new("Specific Enemy"),
        [BuiltInTriggers.PlayerDied.Id.Value] = new("Player Dies"),
        [BuiltInTriggers.PlayerBigHit.Id.Value] = new("Big Hit", "Big Hit Means at Least", "damage", 99),
        [BuiltInTriggers.CurseObtained.Id.Value] = new("Gets a Curse"),
        [BuiltInTriggers.PotionUsed.Id.Value] = new("Uses a Potion"),
        [BuiltInTriggers.TurnEndedWithEnergy.Id.Value] = new("Unspent Energy", "Ending a Turn With at Least", "energy", 9),
        [BuiltInTriggers.RestSiteEntered.Id.Value] = new("Rest Site"),
        [BuiltInTriggers.RunWon.Id.Value] = new("Run Won"),
        [BuiltInTriggers.RunLost.Id.Value] = new("Run Lost"),
    };

    public static TriggerText TextFor(TriggerDefinition trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return Texts.GetValueOrDefault(trigger.Id.Value) ?? new TriggerText(trigger.Id.Value);
    }

    /// <summary>Who can be targeted: subjectless triggers (boss fight) have no "that player".</summary>
    public static IReadOnlyList<TargetKind> TargetsFor(TriggerDefinition trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.HasSubject
            ? [TargetKind.Subject, TargetKind.Everyone, TargetKind.Others, TargetKind.RandomPlayer, TargetKind.RandomOther]
            : [TargetKind.Everyone, TargetKind.RandomPlayer];
    }

    public static string TargetName(TargetKind target) => target switch
    {
        TargetKind.Subject => "That Player",
        TargetKind.Everyone => "Everyone",
        TargetKind.Others => "Everyone Else",
        TargetKind.RandomPlayer => "Random Player",
        TargetKind.RandomOther => "Random Other",
        _ => target.ToString(),
    };

    /// <summary>The rule for a trigger, or (if the set has none) the Standard rule switched off.</summary>
    public static TriggerRule RuleFor(RuleSet rules, TriggerDefinition trigger)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(trigger);
        if (rules.Triggers.TryGetValue(trigger.Id.Value, out var rule))
        {
            return rule;
        }

        return Presets.Standard.Triggers.TryGetValue(trigger.Id.Value, out var standard)
            ? standard with { Enabled = false }
            : TriggerRule.Off(PenaltyKind.Sip, TargetsFor(trigger)[0], threshold: trigger.UsesThreshold ? 1 : null);
    }

    public static RuleSet WithTrigger(RuleSet rules, TriggerDefinition trigger, TriggerRule rule)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(trigger);
        var triggers = new Dictionary<string, TriggerRule>(rules.Triggers, StringComparer.Ordinal) { [trigger.Id.Value] = rule };
        return Named(rules with { Triggers = triggers });
    }

    /// <summary>Index into <see cref="Presets.All"/> of the preset these rules equal (ignoring names), or -1.</summary>
    public static int PresetIndex(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var json = Fingerprint(rules);
        for (var i = 0; i < Presets.All.Count; i++)
        {
            if (Fingerprint(Presets.All[i]) == json)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool IsPresetName(string name) => Presets.All.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    /// <summary>Gives edited rules an honest name (see the class remarks).</summary>
    public static RuleSet Named(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var preset = PresetIndex(rules);
        if (preset >= 0)
        {
            return rules with { Name = Presets.All[preset].Name };
        }

        return IsPresetName(rules.Name) || string.IsNullOrWhiteSpace(rules.Name) ? rules with { Name = CustomName } : rules;
    }

    private static string Fingerprint(RuleSet rules) => RuleSetCodec.ToJson(rules with { Name = string.Empty });
}
