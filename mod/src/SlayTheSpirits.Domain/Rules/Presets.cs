using SlayTheSpirits.Penalties;
using SlayTheSpirits.Triggers;
using static SlayTheSpirits.Rules.TriggerRule;

namespace SlayTheSpirits.Rules;

/// <summary>Built-in rule sets. <see cref="Standard"/> is the default profile.</summary>
public static class Presets
{
    public static RuleSet Standard { get; } = new()
    {
        Name = "Standard",
        Triggers = Rules(
            (BuiltInTriggers.BossEncounterStarted, On(PenaltyKind.Shot, TargetKind.Everyone)),
            (BuiltInTriggers.BossDefeated, On(PenaltyKind.Drink, TargetKind.Everyone)),
            (BuiltInTriggers.EliteEncounterStarted, On(PenaltyKind.Drink, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerDied, On(PenaltyKind.Finish, TargetKind.Subject)),
            (BuiltInTriggers.PlayerBigHit, On(PenaltyKind.Drink, TargetKind.Subject, threshold: 20)),
            (BuiltInTriggers.CurseObtained, On(PenaltyKind.Drink, TargetKind.Subject)),
            (BuiltInTriggers.PotionUsed, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.TurnEndedWithEnergy, Off(PenaltyKind.Sip, TargetKind.Subject, threshold: 1)),
            (BuiltInTriggers.RestSiteEntered, On(PenaltyKind.Water, TargetKind.Everyone)),
            (BuiltInTriggers.RunWon, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.RunLost, On(PenaltyKind.Drink, TargetKind.Everyone))),
        CapsPerAct = new Dictionary<PenaltyKind, int> { [PenaltyKind.Shot] = 3, [PenaltyKind.Chug] = 1 },
    };

    public static RuleSet Casual { get; } = Standard with
    {
        Name = "Casual",
        Triggers = Rules(
            (BuiltInTriggers.BossEncounterStarted, On(PenaltyKind.Drink, TargetKind.Everyone)),
            (BuiltInTriggers.BossDefeated, On(PenaltyKind.Sip, TargetKind.Everyone)),
            (BuiltInTriggers.EliteEncounterStarted, On(PenaltyKind.Sip, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerDied, On(PenaltyKind.Drink, TargetKind.Subject)),
            (BuiltInTriggers.PlayerBigHit, Off(PenaltyKind.Sip, TargetKind.Subject, threshold: 25)),
            (BuiltInTriggers.CurseObtained, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.PotionUsed, Off(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.TurnEndedWithEnergy, Off(PenaltyKind.Sip, TargetKind.Subject, threshold: 1)),
            (BuiltInTriggers.RestSiteEntered, On(PenaltyKind.Water, TargetKind.Everyone)),
            (BuiltInTriggers.RunWon, On(PenaltyKind.Drink, TargetKind.Everyone)),
            (BuiltInTriggers.RunLost, On(PenaltyKind.Sip, TargetKind.Everyone))),
        CapsPerAct = new Dictionary<PenaltyKind, int> { [PenaltyKind.Shot] = 0, [PenaltyKind.Chug] = 0, [PenaltyKind.Finish] = 0 },
    };

    public static RuleSet Degenerate { get; } = Standard with
    {
        Name = "Degenerate",
        Triggers = Rules(
            (BuiltInTriggers.BossEncounterStarted, On(PenaltyKind.Chug, TargetKind.Everyone)),
            (BuiltInTriggers.BossDefeated, On(PenaltyKind.Shot, TargetKind.Everyone)),
            (BuiltInTriggers.EliteEncounterStarted, On(PenaltyKind.Shot, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerDied, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerBigHit, On(PenaltyKind.Drink, TargetKind.Subject, threshold: 12)),
            (BuiltInTriggers.CurseObtained, On(PenaltyKind.Shot, TargetKind.Subject)),
            (BuiltInTriggers.PotionUsed, On(PenaltyKind.Drink, TargetKind.Subject)),
            (BuiltInTriggers.TurnEndedWithEnergy, On(PenaltyKind.Sip, TargetKind.Subject, threshold: 1)),
            (BuiltInTriggers.RestSiteEntered, On(PenaltyKind.Water, TargetKind.Everyone)),
            (BuiltInTriggers.RunWon, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.RunLost, On(PenaltyKind.Finish, TargetKind.Everyone))),
        CapsPerAct = new Dictionary<PenaltyKind, int> { [PenaltyKind.Shot] = 5, [PenaltyKind.Chug] = 2 },
    };

    /// <summary>Same structure as <see cref="Standard"/>, relabeled for a non-alcoholic table.</summary>
    public static RuleSet Sober { get; } = Standard with
    {
        Name = "Sober",
        Labels = new Dictionary<PenaltyKind, PenaltyLabel>
        {
            [PenaltyKind.Sip] = new("Sip of soda", "Sips of soda"),
            [PenaltyKind.Drink] = new("Drink of soda", "Drinks of soda"),
            [PenaltyKind.Shot] = new("Shot of pickle juice", "Shots of pickle juice"),
            [PenaltyKind.Chug] = new("Chug your soda", "Chug your soda"),
            [PenaltyKind.Finish] = new("Finish your soda", "Finish your soda"),
        },
    };

    public static IReadOnlyList<RuleSet> All { get; } = [Casual, Standard, Degenerate, Sober];

    private static Dictionary<string, TriggerRule> Rules(params (TriggerDefinition Trigger, TriggerRule Rule)[] rules) =>
        rules.ToDictionary(static r => r.Trigger.Id.Value, static r => r.Rule, StringComparer.Ordinal);
}
