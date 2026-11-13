using SlayTheSpirits.Party;

namespace SlayTheSpirits.Triggers;

/// <summary>Identifier of a kind of game moment that can cause a penalty, e.g. <c>encounter.boss.start</c>.</summary>
public readonly record struct TriggerId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Something that happened in the game, already translated out of game types.
/// </summary>
/// <param name="Trigger">Which kind of moment this is.</param>
/// <param name="Subject">The player it happened to, if any (who died, who got hit).</param>
/// <param name="Detail">Extra identifying data, e.g. the monster id for <see cref="BuiltInTriggers.EnemyEncountered"/>.</param>
/// <param name="Magnitude">A number compared to the rule's threshold, e.g. damage taken or unspent energy.</param>
/// <param name="Label">Human-readable context for the announcement, e.g. "Gremlin Nob".</param>
public sealed record TriggerOccurrence(
    TriggerId Trigger,
    PlayerId? Subject = null,
    string? Detail = null,
    int? Magnitude = null,
    string? Label = null);

/// <summary>Static description of a trigger, used for validation and the settings UI.</summary>
/// <param name="Id">The trigger id.</param>
/// <param name="HasSubject">Whether occurrences carry a <see cref="TriggerOccurrence.Subject"/>.</param>
/// <param name="UsesThreshold">Whether rules compare <see cref="TriggerOccurrence.Magnitude"/> to a threshold.</param>
/// <param name="IsKeyedByDetail">Whether rules live in a per-detail table (e.g. per monster) instead of one rule.</param>
public sealed record TriggerDefinition(TriggerId Id, bool HasSubject, bool UsesThreshold = false, bool IsKeyedByDetail = false);

/// <summary>The triggers this mod raises from game events.</summary>
public static class BuiltInTriggers
{
    public static readonly TriggerDefinition BossEncounterStarted = new(new("encounter.boss.start"), HasSubject: false);
    public static readonly TriggerDefinition BossDefeated = new(new("encounter.boss.won"), HasSubject: false);
    public static readonly TriggerDefinition EliteEncounterStarted = new(new("encounter.elite.start"), HasSubject: false);

    /// <summary>Raised once per distinct monster id when combat starts. Rules are keyed by monster id.</summary>
    public static readonly TriggerDefinition EnemyEncountered = new(new("encounter.enemy"), HasSubject: false, IsKeyedByDetail: true);

    public static readonly TriggerDefinition PlayerDied = new(new("player.died"), HasSubject: true);

    /// <summary>Magnitude = unblocked damage from a single hit.</summary>
    public static readonly TriggerDefinition PlayerBigHit = new(new("player.bigHit"), HasSubject: true, UsesThreshold: true);

    public static readonly TriggerDefinition CurseObtained = new(new("player.curseObtained"), HasSubject: true);
    public static readonly TriggerDefinition PotionUsed = new(new("player.potionUsed"), HasSubject: true);

    /// <summary>Magnitude = energy left when the player ended their turn.</summary>
    public static readonly TriggerDefinition TurnEndedWithEnergy = new(new("player.turnEndedWithEnergy"), HasSubject: true, UsesThreshold: true);

    public static readonly TriggerDefinition RestSiteEntered = new(new("run.restSite"), HasSubject: false);
    public static readonly TriggerDefinition RunWon = new(new("run.won"), HasSubject: false);
    public static readonly TriggerDefinition RunLost = new(new("run.lost"), HasSubject: false);

    public static IReadOnlyList<TriggerDefinition> All { get; } =
    [
        BossEncounterStarted, BossDefeated, EliteEncounterStarted, EnemyEncountered,
        PlayerDied, PlayerBigHit, CurseObtained, PotionUsed, TurnEndedWithEnergy,
        RestSiteEntered, RunWon, RunLost,
    ];

    private static readonly Dictionary<string, TriggerDefinition> ById = All.ToDictionary(static d => d.Id.Value, StringComparer.Ordinal);

    public static TriggerDefinition? Find(string id) => ById.GetValueOrDefault(id);
}
