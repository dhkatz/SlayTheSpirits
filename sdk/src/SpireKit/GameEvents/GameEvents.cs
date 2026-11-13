using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SpireKit.GameEvents;

// Normalized game events published on ModContext.Events.
//
// Hook-driven events are published with PublishAsync and awaited by the game's hook dispatch, so
// SubscribeAsync handlers may issue game commands (PowerCmd, CreatureCmd...) and they run in lockstep.
//
// The SDK's job here is *when* and *how often* these fire, not hiding game types: it deduplicates
// hooks that the game dispatches twice, fills gaps (e.g. lethal hits skip AfterDamageReceived), and
// suppresses noise (e.g. the force-kill of every player when a run ends). Every event fires on every
// peer, in the same order, because they come from the lockstep-simulated hook system.

/// <summary>Marker for every SDK game event.</summary>
public interface IGameEvent
{
    IRunState Run { get; }
}

/// <summary>How a run began.</summary>
public enum RunStartKind
{
    New,
    Loaded,
}

/// <summary>A run is live (new or resumed from a save). Fires on every peer.</summary>
public sealed record RunStarted(IRunState Run, RunStartKind Kind) : IGameEvent;

/// <summary>A run finished. <paramref name="Abandoned"/> runs count as defeats.</summary>
public sealed record RunEnded(IRunState Run, bool Victory, bool Abandoned) : IGameEvent;

/// <summary>Players entered an act. <paramref name="ActIndex"/> is 0-based.</summary>
public sealed record ActEntered(IRunState Run, int ActIndex) : IGameEvent;

/// <summary>Players entered a room (rest site, shop, combat, event…).</summary>
public sealed record RoomEntered(IRunState Run, AbstractRoom Room) : IGameEvent
{
    public RoomType RoomType => Room.RoomType;
}

/// <summary>Combat is set up and about to begin; all initial enemies are present.</summary>
public sealed record CombatStarted(IRunState Run, CombatState Combat, EncounterModel Encounter, IReadOnlyList<MonsterModel> Monsters) : IGameEvent
{
    public RoomType RoomType => Encounter.RoomType;
}

/// <summary>Combat was won. <paramref name="IsFinalBoss"/> is true for the last act's boss.</summary>
public sealed record CombatWon(IRunState Run, CombatRoom Room, bool IsFinalBoss) : IGameEvent
{
    public RoomType RoomType => Room.RoomType;
}

/// <summary>A player's character died (not prevented, not the end-of-run cleanup kill).</summary>
public sealed record PlayerDied(IRunState Run, Player Player, PlayerChoiceContext? ChoiceContext = null) : IGameEvent;

/// <summary>A player lost HP from one hit, in or out of combat, including the killing blow.</summary>
/// <param name="Run">The run.</param>
/// <param name="Player">Who was hit.</param>
/// <param name="HpLost">Unblocked damage actually removed from HP.</param>
/// <param name="Dealer">The attacker, if any.</param>
/// <param name="WasLethal">True if this hit killed the player.</param>
/// <param name="ChoiceContext">The hook's choice context, for issuing game commands.</param>
public sealed record PlayerDamaged(IRunState Run, Player Player, int HpLost, Creature? Dealer, bool WasLethal, PlayerChoiceContext? ChoiceContext = null) : IGameEvent;

/// <summary>A new card entered a player's deck (rewards, shops, events, curses…).</summary>
public sealed record CardAddedToDeck(IRunState Run, Player Player, CardModel Card) : IGameEvent;

/// <summary>A player is using a potion.</summary>
public sealed record PotionUsed(IRunState Run, Player Player, PotionModel Potion, Creature? Target) : IGameEvent;

/// <summary>All players committed to ending the turn; energy has not been reset yet.</summary>
public sealed record PlayerTurnEnding(IRunState Run, Player Player, int EnergyLeft, PlayerChoiceContext? ChoiceContext = null) : IGameEvent;
