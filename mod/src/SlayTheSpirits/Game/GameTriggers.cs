using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheSpirits.Party;
using SlayTheSpirits.Potions;
using SlayTheSpirits.Triggers;
using SpireKit.GameEvents;

namespace SlayTheSpirits.Game;

/// <summary>Translates SpireKit game events into domain <see cref="TriggerOccurrence"/>s. No state, no side effects.</summary>
internal static class GameTriggers
{
    public static IEnumerable<TriggerOccurrence> From(IGameEvent gameEvent) => gameEvent switch
    {
        CombatStarted e => FromCombatStarted(e),
        CombatWon { RoomType: RoomType.Boss } e => [new(BuiltInTriggers.BossDefeated.Id, Label: Text(e.Room.Encounter.Title))],
        PlayerDied e => [new(BuiltInTriggers.PlayerDied.Id, Id(e.Player))],
        PlayerDamaged e => [new(BuiltInTriggers.PlayerBigHit.Id, Id(e.Player), Magnitude: e.HpLost, Label: e.Dealer?.Name)],
        CardAddedToDeck { Card.Type: CardType.Curse } e => [new(BuiltInTriggers.CurseObtained.Id, Id(e.Player), Label: e.Card.Title)],
        PotionUsed { Potion: SpiritsPotion } => [], // The potion is the drink; don't add a second one.
        PotionUsed e => [new(BuiltInTriggers.PotionUsed.Id, Id(e.Player), Label: Text(e.Potion.Title))],
        PlayerTurnEnding { EnergyLeft: > 0 } e => [new(BuiltInTriggers.TurnEndedWithEnergy.Id, Id(e.Player), Magnitude: e.EnergyLeft)],
        RoomEntered { RoomType: RoomType.RestSite } => [new(BuiltInTriggers.RestSiteEntered.Id)],
        RunEnded { Victory: true } => [new(BuiltInTriggers.RunWon.Id)],
        RunEnded => [new(BuiltInTriggers.RunLost.Id)],
        _ => [],
    };

    public static PlayerId Id(Player player) => new(player.NetId);

    private static IEnumerable<TriggerOccurrence> FromCombatStarted(CombatStarted e)
    {
        var encounter = Text(e.Encounter.Title);
        switch (e.RoomType)
        {
            case RoomType.Boss:
                yield return new(BuiltInTriggers.BossEncounterStarted.Id, Label: encounter);
                break;
            case RoomType.Elite:
                yield return new(BuiltInTriggers.EliteEncounterStarted.Id, Label: encounter);
                break;
        }

        foreach (var monster in e.Monsters.DistinctBy(static m => m.Id))
        {
            yield return new(BuiltInTriggers.EnemyEncountered.Id, Detail: monster.Id.ToString(), Label: Text(monster.Title));
        }
    }

    /// <summary>Formatted text, or null if it can't be read (missing localization must not break gameplay).</summary>
    public static string? Text(LocString? text)
    {
        try
        {
            return text?.GetFormattedText();
        }
#pragma warning disable CA1031 // Missing localization must not break gameplay.
        catch
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
