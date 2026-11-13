using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Party;
using SlayTheSpirits.Presentation;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using SpireKit.GameEvents;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SlayTheSpirits.Game;

/// <summary>
/// Runs the drinking game for the current run: owns the <see cref="PenaltyEngine"/>, feeds it game events and
/// card effects, keeps the bar tab and Tipsy buff in sync, and hands results to the <see cref="Announcer"/>.
/// </summary>
/// <remarks>
/// Every input arrives through the lockstep-simulated game (hooks and card plays), so every peer computes the
/// same penalties independently; nothing here sends network messages. Handlers are async and awaited by the
/// game's hook dispatch, so Tipsy power changes happen inside the game's own action flow.
/// </remarks>
internal sealed class DrinkGame
{
    private readonly RulesService _rules;
    private readonly Announcer _announcer;
    private readonly IModLog _log;
    private IRunState? _run;
    private string? _runKey;
    private PenaltyEngine? _engine;

    public DrinkGame(ModContext context, RulesService rules, Announcer announcer)
    {
        _rules = rules;
        _announcer = announcer;
        _log = context.Log.WithScope("game");
        context.Events.SubscribeAsync<IGameEvent>(OnGameEventAsync);
    }

    /// <summary>The current run's tally, for tooltips and the Bar Tab. Null outside a run.</summary>
    public RunTally? Tally => _engine?.State.Tally;

    /// <summary>A player's Tipsy this run (0 outside a run).</summary>
    public int TipsyOf(PlayerId player) => _engine?.TipsyOf(player) ?? 0;

    /// <summary>Hands out an explicit penalty (cards, relics, potions) and updates Tipsy. Await it from <c>OnPlay</c>.</summary>
    public Task PenalizeAsync(PenaltyRequest request, PlayerChoiceContext? choiceContext) =>
        EnsureEngine() is { } engine && _run is { } run
            ? DeliverAsync(run, engine.Handle(request, Party(run)), choiceContext, CombatPhase.Ongoing)
            : Task.CompletedTask;

    /// <summary>Grants immunity until the end of combat (Designated Driver).</summary>
    public void GrantImmunity(PlayerId player) => EnsureEngine()?.GrantImmunity(player);

    /// <summary>Passes the player's next penalty to someone else (Slip Away).</summary>
    public void AddRedirect(PlayerId player) => EnsureEngine()?.AddRedirects(player);

    private async Task OnGameEventAsync(IGameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case RunStarted started:
                OnRunStarted(started);
                return;
            case RunEnded ended:
                await ApplyAsync(GameTriggers.From(ended), null, CombatPhase.None).ConfigureAwait(true);
                OnRunEnded();
                return;
            case ActEntered act:
                _engine?.EnterAct(act.ActIndex + 1);
                return;
            case CombatStarted:
                if (EnsureEngine() is { } engine && _run is { } run)
                {
                    // Immunity lasts one combat. It's also cleared on victory, but a combat can end without
                    // one (the dev console's `room`, escapes), and the power that showed it is gone by now.
                    engine.ClearImmunities();
                    await TipsyBuff.RestoreAsync(run, engine).ConfigureAwait(true);
                }

                await ApplyAsync(GameTriggers.From(gameEvent), null, CombatPhase.Starting).ConfigureAwait(true);
                return;
            case CombatWon:
                _engine?.ClearImmunities();
                break;
        }

        var choiceContext = gameEvent switch
        {
            PlayerDamaged e => e.ChoiceContext,
            PlayerDied e => e.ChoiceContext,
            PlayerTurnEnding e => e.ChoiceContext,
            _ => null,
        };
        await ApplyAsync(GameTriggers.From(gameEvent), choiceContext, CombatPhase.Ongoing).ConfigureAwait(true);
    }

    private void OnRunStarted(RunStarted e)
    {
        _run = e.Run;
        _runKey = e.Run.Rng.StringSeed;
        _engine = null;
        _rules.LockForRun(_runKey, resumed: e.Kind == RunStartKind.Loaded);
        _announcer.AnnounceRunStart(_rules.Effective);
        if (e.Kind == RunStartKind.Loaded)
        {
            _log.Info("Resumed run: penalty caps and tally restart from zero (rules are restored).");
        }
    }

    private void OnRunEnded()
    {
        if (_engine is { } engine && _run is { } run)
        {
            _announcer.AnnounceBarTab(engine.State.Tally, Party(run));
        }

        if (_runKey is { } key)
        {
            _rules.UnlockAfterRun(key);
        }

        _engine = null;
        _run = null;
        _runKey = null;
    }

    private async Task ApplyAsync(IEnumerable<TriggerOccurrence> occurrences, PlayerChoiceContext? choiceContext, CombatPhase phase)
    {
        if (_run is not { } run)
        {
            return;
        }

        foreach (var occurrence in occurrences.ToList())
        {
            if (EnsureEngine() is not { } engine)
            {
                return;
            }

            await DeliverAsync(run, engine.Handle(occurrence, Party(run)), choiceContext, phase).ConfigureAwait(true);
        }
    }

    private async Task DeliverAsync(IRunState run, IReadOnlyList<PenaltyAssignment> assignments, PlayerChoiceContext? choiceContext, CombatPhase phase)
    {
        if (assignments.Count == 0)
        {
            return;
        }

        _announcer.Announce(run, assignments, Party(run), phase);
        await BarTab.AddAsync(run, assignments, choiceContext).ConfigureAwait(true);
        await TipsyBuff.AddAsync(run, assignments, choiceContext).ConfigureAwait(true);
    }

    /// <summary>
    /// The engine is created lazily, once both a run and rules exist. A client that hasn't received the host's
    /// rules yet simply sits out (and logs it) rather than guessing.
    /// </summary>
    private PenaltyEngine? EnsureEngine()
    {
        if (_engine is not null || _run is not { } run)
        {
            return _engine;
        }

        if (_rules.Effective is not { } rules)
        {
            _log.Warn("Skipping penalty: rules from the host haven't arrived yet.");
            return null;
        }

        _engine = new PenaltyEngine(rules, new RunPenaltyState { Act = run.CurrentActIndex + 1 }, run.Rng.Seed);
        _log.Info($"Drinking game on: '{rules.Name}'.");
        return _engine;
    }

    /// <summary>Party in slot order (identical on every peer).</summary>
    private static PartySnapshot Party(IRunState run) => new(
        [.. run.Players
            .OrderBy(run.GetPlayerSlotIndex)
            .Select(static p => new PartyMember(GameTriggers.Id(p), p.Creature.Name, p.Creature.IsAlive))]);
}

/// <summary>Where we are relative to combat, for presentation timing.</summary>
internal enum CombatPhase
{
    /// <summary>Not in combat (map, events, rest sites, run end).</summary>
    None,

    /// <summary>Combat is starting; the game's own start banners are about to play.</summary>
    Starting,

    /// <summary>Mid-combat, or anything else.</summary>
    Ongoing,
}
