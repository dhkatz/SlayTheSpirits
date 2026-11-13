using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SpireKit.Diagnostics;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.GameEvents;

/// <summary>Translates raw hook calls into normalized <see cref="IGameEvent"/>s on the mod's event bus.</summary>
public sealed class GameEventBridge
{
    private const string Feature = "events.hooks";

    private readonly ModContext _context;
    private readonly Type _listenerType;
    private SpireHookListener? _runListener;
    private SpireHookListener? _combatListener;

    private GameEventBridge(ModContext context, Type listenerType)
    {
        _context = context;
        _listenerType = listenerType;
    }

    private bool _listenerFailed;
    private RunStartKind? _pendingStartKind;
    private IRunState? _currentRun;

    internal static GameEventBridge? Instance { get; private set; }

    internal static void Install(ModContext context, Assembly modAssembly)
    {
        var listenerTypes = modAssembly.GetTypes()
            .Where(static t => t is { IsAbstract: false } && t.IsSubclassOf(typeof(SpireHookListener)))
            .ToList();
        if (listenerTypes.Count != 1)
        {
            var reason = listenerTypes.Count == 0
                ? $"declare exactly one listener: public sealed class {context.Mod.ModelPrefix}HookListener : {nameof(SpireHookListener)};"
                : $"found {listenerTypes.Count} {nameof(SpireHookListener)} subclasses; declare exactly one";
            context.Health.Report(context.Id, Feature, FeatureStatus.Disabled, reason);
            context.Log.Error($"Game events disabled: {reason}");
            return;
        }

        var bridge = new GameEventBridge(context, listenerTypes[0]);
        Instance = bridge;

        // Subscription ids are sorted ordinally by the game; prefixing with the mod id keeps them unique per mod.
        ModHelper.SubscribeForRunStateHooks($"{context.Id}.spirekit.run", bridge.RunListeners);
        ModHelper.SubscribeForCombatStateHooks($"{context.Id}.spirekit.combat", bridge.CombatListeners);
        context.Health.Report(context.Id, Feature, FeatureStatus.Healthy);
    }

    /// <summary>Subscribes to run start once the game is initialized.</summary>
    internal void AttachToRunManager()
    {
        RunManager.Instance.RunStarted += run => Guard(() =>
        {
            _currentRun = run;
            var kind = _pendingStartKind ?? RunStartKind.New;
            _pendingStartKind = null;
            _ = Publish(new RunStarted(run, kind));
        });
    }

    internal void SetPendingStartKind(RunStartKind kind) => _pendingStartKind = kind;

    internal void OnRunEnded(bool victory, bool abandoned) => Guard(() =>
    {
        if (_currentRun is { } run)
        {
            _ = Publish(new RunEnded(run, victory, abandoned));
        }
    });

    /// <summary>Publishes to sync and async subscribers; the returned task completes when all handlers have.</summary>
    internal Task Publish<TEvent>(TEvent gameEvent)
        where TEvent : IGameEvent => _context.Events.PublishAsync(gameEvent);

    /// <summary>Runs <paramref name="action"/>, logging instead of throwing into the game's code.</summary>
    internal void Guard(Action action)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031 // An exception here would abort the game's hook dispatch.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _context.Log.Error("Game event handler failed.", ex);
        }
    }

    /// <summary>Async <see cref="Guard"/>: the game awaits this inside its hook loop, so it must never fault.</summary>
    internal async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _context.Log.Error("Game event handler failed.", ex);
        }
    }

    internal Task OnActEntered(IRunState run) => Publish(new ActEntered(run, run.CurrentActIndex));

    internal Task OnRoomEntered(IRunState run, AbstractRoom room) => Publish(new RoomEntered(run, room));

    internal Task OnCombatStarting(IRunState run)
    {
        if (run.CurrentRoom is not CombatRoom room)
        {
            return Task.CompletedTask;
        }

        var combat = room.CombatState;
        var monsters = combat.Enemies.Select(static c => c.Monster).OfType<MonsterModel>().ToList();
        return Publish(new CombatStarted(run, combat, room.Encounter, monsters));
    }

    internal Task OnCombatWon(IRunState run, CombatRoom room)
    {
        var isFinalBoss = room.RoomType == RoomType.Boss && run.CurrentActIndex == run.Acts.Count - 1;
        return Publish(new CombatWon(run, room, isFinalBoss));
    }

    internal Task OnDeath(IRunState run, PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented)
    {
        // Winning or abandoning a run force-kills every player (after OnEnded / IsAbandoned); those aren't deaths.
        var runManager = RunManager.Instance;
        if (wasRemovalPrevented || runManager.HasRunEnded || runManager.IsAbandoned || creature.Player is not { } player)
        {
            return Task.CompletedTask;
        }

        return Publish(new PlayerDied(run, player, choiceContext));
    }

    /// <summary>Non-lethal hits (the game skips this hook when the hit kills).</summary>
    internal Task OnDamageReceived(IRunState run, PlayerChoiceContext choiceContext, Creature target, DamageResult result, Creature? dealer) =>
        target.Player is { } player && result.UnblockedDamage > 0 && !result.WasTargetKilled
            ? Publish(new PlayerDamaged(run, player, result.UnblockedDamage, dealer, WasLethal: false, choiceContext))
            : Task.CompletedTask;

    /// <summary>Lethal hits only; non-lethal ones arrive via <see cref="OnDamageReceived"/>.</summary>
    internal Task OnDamageGiven(IRunState run, PlayerChoiceContext choiceContext, Creature target, DamageResult result, Creature? dealer) =>
        target.Player is { } player && result.WasTargetKilled && result.UnblockedDamage > 0
            ? Publish(new PlayerDamaged(run, player, result.UnblockedDamage, dealer, WasLethal: true, choiceContext))
            : Task.CompletedTask;

    internal Task OnCardChangedPiles(IRunState run, CardModel card, PileType oldPileType) =>
        oldPileType == PileType.None && card.Pile?.Type == PileType.Deck
            ? Publish(new CardAddedToDeck(run, card.Owner, card))
            : Task.CompletedTask;

    internal Task OnPotionUsed(IRunState run, PotionModel potion, Creature? target) =>
        Publish(new PotionUsed(run, potion.Owner, potion, target));

    internal async Task OnSideTurnEnding(IRunState run, PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }

        foreach (var creature in participants.ToList())
        {
            if (creature.Player is { PlayerCombatState: { } combatState } player)
            {
                await Publish(new PlayerTurnEnding(run, player, combatState.Energy, choiceContext)).ConfigureAwait(true);
            }
        }
    }

    private IEnumerable<AbstractModel> RunListeners(RunState runState)
    {
        _runListener ??= CreateListener(SpireHookListener.ListenerScope.Run);
        if (_runListener is null)
        {
            return [];
        }

        _runListener.Run = runState;
        return [_runListener];
    }

    private IEnumerable<AbstractModel> CombatListeners(CombatState combatState)
    {
        _combatListener ??= CreateListener(SpireHookListener.ListenerScope.Combat);
        if (_combatListener is null)
        {
            return [];
        }

        _combatListener.Run = combatState.RunState;
        return [_combatListener];
    }

    private SpireHookListener? CreateListener(SpireHookListener.ListenerScope scope)
    {
        if (_listenerFailed)
        {
            return null;
        }

        try
        {
            var canonical = ModelDb.GetById<SpireHookListener>(ModelDb.GetId(_listenerType));
            var listener = (SpireHookListener)canonical.MutableClone();
            listener.Scope = scope;
            return listener;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _listenerFailed = true;
            _context.Log.Error($"Could not create hook listener {_listenerType.Name}.", ex);
            _context.Health.Report(_context.Id, Feature, FeatureStatus.Disabled, ex.Message);
            return null;
        }
    }
}
