using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireKit.GameEvents;

/// <summary>
/// Receives the game's hooks and forwards them to <see cref="GameEventBridge"/>.
/// </summary>
/// <remarks>
/// <para>
/// The game only dispatches hooks to <see cref="AbstractModel"/>s it knows about, and model ids come from
/// class names. Because SpireKit is embedded in every mod, each mod declares its own one-line subclass so
/// ids never collide: <c>public sealed class MyModHookListener : SpireHookListener;</c>
/// </para>
/// <para>
/// Two clones are subscribed: one to run-state hooks and one to combat-state hooks. Hooks the game sends
/// to both lists are only handled by the run clone, so nothing is processed twice.
/// </para>
/// </remarks>
public abstract class SpireHookListener : AbstractModel
{
    internal enum ListenerScope
    {
        Run,
        Combat,
    }

    internal ListenerScope Scope { get; set; }

    internal IRunState? Run { get; set; }

    public override bool ShouldReceiveCombatHooks => true;

    private bool IsRunScope => Scope == ListenerScope.Run;

    private bool IsCombatScope => Scope == ListenerScope.Combat;

    // ---- Run-scoped hooks (run-only, or dispatched to both lists) ----
    public override Task AfterActEntered() => Forward(IsRunScope, static (b, run) => b.OnActEntered(run));

    public override Task AfterRoomEntered(AbstractRoom room) => Forward(IsRunScope, (b, run) => b.OnRoomEntered(run, room));

    public override Task BeforeCombatStart() => Forward(IsRunScope, static (b, run) => b.OnCombatStarting(run));

    public override Task AfterCombatVictory(CombatRoom room) => Forward(IsRunScope, (b, run) => b.OnCombatWon(run, room));

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength) =>
        Forward(IsRunScope, (b, run) => b.OnDeath(run, choiceContext, creature, wasRemovalPrevented));

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Forward(IsRunScope, (b, run) => b.OnDamageReceived(run, choiceContext, target, result, dealer));

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
        Forward(IsRunScope, (b, run) => b.OnCardChangedPiles(run, card, oldPileType));

    public override Task BeforePotionUsed(PotionModel potion, Creature? target) =>
        Forward(IsRunScope, (b, run) => b.OnPotionUsed(run, potion, target));

    // ---- Combat-only hooks ----
    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource) =>
        Forward(IsCombatScope, (b, run) => b.OnDamageGiven(run, choiceContext, target, result, dealer));

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants) =>
        Forward(IsCombatScope, (b, run) => b.OnSideTurnEnding(run, choiceContext, side, participants));

    /// <summary>
    /// Returns the bridge's task so the game awaits mod handlers inside its own hook dispatch, which keeps
    /// any game commands they issue (apply power, gain block) in lockstep on every peer.
    /// </summary>
    private Task Forward(bool handles, Func<GameEventBridge, IRunState, Task> forward) =>
        handles && Run is { } run && GameEventBridge.Instance is { } bridge
            ? bridge.GuardAsync(() => forward(bridge, run))
            : Task.CompletedTask;
}
