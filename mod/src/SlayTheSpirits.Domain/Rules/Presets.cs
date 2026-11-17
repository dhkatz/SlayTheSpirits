using SlayTheSpirits.Penalties;
using SlayTheSpirits.Triggers;
using static SlayTheSpirits.Rules.TriggerRule;

namespace SlayTheSpirits.Rules;

/// <summary>Built-in rule sets. <see cref="Standard"/> is the default profile.</summary>
public static class Presets
{
    /// <summary>
    /// The default: sips for small things, finish your drink when you die, everyone chugs a beer when a boss
    /// shows up, and a shot only when the run is lost.
    /// </summary>
    public static RuleSet Standard { get; } = new()
    {
        Name = "Standard",
        Triggers = Rules(
            (BuiltInTriggers.BossEncounterStarted, On(PenaltyKind.Chug, TargetKind.Everyone)),
            (BuiltInTriggers.BossDefeated, On(PenaltyKind.Sip, TargetKind.Everyone)),
            (BuiltInTriggers.EliteEncounterStarted, On(PenaltyKind.Sip, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerDied, On(PenaltyKind.Finish, TargetKind.Subject)),
            (BuiltInTriggers.PlayerBigHit, On(PenaltyKind.Sip, TargetKind.Subject, threshold: 20)),
            (BuiltInTriggers.CurseObtained, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.PotionUsed, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.TurnEndedWithEnergy, Off(PenaltyKind.Sip, TargetKind.Subject, threshold: 1)),
            (BuiltInTriggers.RestSiteEntered, On(PenaltyKind.Water, TargetKind.Everyone)),
            (BuiltInTriggers.RunWon, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.RunLost, On(PenaltyKind.Shot, TargetKind.Everyone))),
        CapsPerAct = new Dictionary<PenaltyKind, int> { [PenaltyKind.Finish] = 2, [PenaltyKind.Chug] = 2, [PenaltyKind.Shot] = 1 },
    };

    /// <summary>Sips and the odd finished drink; never a chug or a shot (caps turn them into Finish).</summary>
    public static RuleSet Casual { get; } = Standard with
    {
        Name = "Casual",
        Triggers = Rules(
            (BuiltInTriggers.BossEncounterStarted, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.BossDefeated, On(PenaltyKind.Sip, TargetKind.Everyone)),
            (BuiltInTriggers.EliteEncounterStarted, On(PenaltyKind.Sip, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerDied, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.PlayerBigHit, Off(PenaltyKind.Sip, TargetKind.Subject, threshold: 25)),
            (BuiltInTriggers.CurseObtained, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.PotionUsed, Off(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.TurnEndedWithEnergy, Off(PenaltyKind.Sip, TargetKind.Subject, threshold: 1)),
            (BuiltInTriggers.RestSiteEntered, On(PenaltyKind.Water, TargetKind.Everyone)),
            (BuiltInTriggers.RunWon, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.RunLost, On(PenaltyKind.Finish, TargetKind.Everyone))),
        CapsPerAct = new Dictionary<PenaltyKind, int> { [PenaltyKind.Finish] = 1, [PenaltyKind.Chug] = 0, [PenaltyKind.Shot] = 0 },
        RestSiteTabs = RestSiteTabLimit.Off,
        Taverns = TavernLimit.Off,
    };

    /// <summary>Elites and curses empty glasses, deaths cost a shot, and the run's end is a shot either way.</summary>
    public static RuleSet Degenerate { get; } = Standard with
    {
        Name = "Degenerate",
        Triggers = Rules(
            (BuiltInTriggers.BossEncounterStarted, On(PenaltyKind.Chug, TargetKind.Everyone)),
            (BuiltInTriggers.BossDefeated, On(PenaltyKind.Sip, TargetKind.Everyone, count: 2)),
            (BuiltInTriggers.EliteEncounterStarted, On(PenaltyKind.Finish, TargetKind.Everyone)),
            (BuiltInTriggers.PlayerDied, On(PenaltyKind.Shot, TargetKind.Subject)),
            (BuiltInTriggers.PlayerBigHit, On(PenaltyKind.Sip, TargetKind.Subject, threshold: 12)),
            (BuiltInTriggers.CurseObtained, On(PenaltyKind.Finish, TargetKind.Subject)),
            (BuiltInTriggers.PotionUsed, On(PenaltyKind.Sip, TargetKind.Subject)),
            (BuiltInTriggers.TurnEndedWithEnergy, On(PenaltyKind.Sip, TargetKind.Subject, threshold: 1)),
            (BuiltInTriggers.RestSiteEntered, On(PenaltyKind.Water, TargetKind.Everyone)),
            (BuiltInTriggers.RunWon, On(PenaltyKind.Shot, TargetKind.Everyone)),
            (BuiltInTriggers.RunLost, On(PenaltyKind.Shot, TargetKind.Everyone))),
        CapsPerAct = new Dictionary<PenaltyKind, int> { [PenaltyKind.Finish] = 4, [PenaltyKind.Chug] = 3, [PenaltyKind.Shot] = 2 },
        RestSiteTabs = RestSiteTabLimit.Unlimited,
        Taverns = TavernLimit.EveryShop,
    };

    /// <summary>Same structure as <see cref="Standard"/>, relabeled for a non-alcoholic table.</summary>
    public static RuleSet Sober { get; } = Standard with
    {
        Name = "Sober",
        Labels = new Dictionary<PenaltyKind, PenaltyLabel>
        {
            [PenaltyKind.Sip] = new("Sip of soda", "Sips of soda"),
            [PenaltyKind.Finish] = new("Finish your soda", "Finish your soda"),
            [PenaltyKind.Chug] = new("Chug a soda", "Chug sodas"),
            [PenaltyKind.Shot] = new("Shot of pickle juice", "Shots of pickle juice"),
        },
    };

    public static IReadOnlyList<RuleSet> All { get; } = [Casual, Standard, Degenerate, Sober];

    private static Dictionary<string, TriggerRule> Rules(params (TriggerDefinition Trigger, TriggerRule Rule)[] rules) =>
        rules.ToDictionary(static r => r.Trigger.Id.Value, static r => r.Rule, StringComparer.Ordinal);
}
