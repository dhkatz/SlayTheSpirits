using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Powers;

namespace SlayTheSpirits.Game;

/// <summary>
/// Adds freshly assigned penalties to each player's per-combat bar tab (<see cref="SpiritsTabPower"/>s).
/// Counts what was actually assigned, after caps, immunity and redirects. Outside combat this is a no-op.
/// </summary>
internal static class BarTab
{
    public static async Task AddAsync(IRunState run, IReadOnlyList<PenaltyAssignment> assignments, PlayerChoiceContext? choiceContext)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        foreach (var group in assignments.GroupBy(static a => (a.Player, a.Penalty.Kind)))
        {
            if (run.GetPlayer(group.Key.Player.Value)?.Creature is { CombatState: not null } creature)
            {
                var context = choiceContext ?? new ThrowingPlayerChoiceContext();
                var amount = group.Sum(static a => a.Penalty.Count);
                await (group.Key.Kind switch
                {
                    PenaltyKind.Water => ApplyAsync<SpiritsWaterTabPower>(context, creature, amount),
                    PenaltyKind.Sip => ApplyAsync<SpiritsSipTabPower>(context, creature, amount),
                    PenaltyKind.Shot => ApplyAsync<SpiritsShotTabPower>(context, creature, amount),
                    PenaltyKind.Chug => ApplyAsync<SpiritsChugTabPower>(context, creature, amount),
                    PenaltyKind.Finish => ApplyAsync<SpiritsFinishTabPower>(context, creature, amount),
                    _ => Task.CompletedTask,
                }).ConfigureAwait(true);
            }
        }
    }

    private static async Task ApplyAsync<T>(PlayerChoiceContext context, Creature creature, int amount)
        where T : SpiritsTabPower =>
        await PowerCmd.Apply<T>(context, creature, amount, null, null).ConfigureAwait(true);
}
