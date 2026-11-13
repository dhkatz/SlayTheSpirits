using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Party;
using SlayTheSpirits.Powers;

namespace SlayTheSpirits.Game;

/// <summary>
/// Keeps each player's <see cref="SpiritsTipsyPower"/> equal to their run-long Tipsy during combat.
/// All calls happen inside game hooks or card plays, so they run in lockstep on every peer.
/// </summary>
internal static class TipsyBuff
{
    /// <summary>Combat just started (powers were wiped): give everyone their running total.</summary>
    public static async Task RestoreAsync(IRunState run, PenaltyEngine engine)
    {
        foreach (var player in run.Players)
        {
            var creature = player.Creature;
            var amount = engine.TipsyOf(new PlayerId(player.NetId));
            if (amount > 0 && creature.CombatState is not null && !creature.HasPower<SpiritsTipsyPower>())
            {
                await PowerCmd.Apply<SpiritsTipsyPower>(new ThrowingPlayerChoiceContext(), creature, amount, null, null, silent: true);
            }
        }
    }

    /// <summary>
    /// Adds the Tipsy from freshly assigned penalties to each player's buff (no-op outside combat). Silent:
    /// the bar tab counter flashes for the same penalty, and one flash per drink reads better than two.
    /// </summary>
    public static async Task AddAsync(IRunState run, IReadOnlyList<PenaltyAssignment> assignments, PlayerChoiceContext? choiceContext)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        foreach (var group in assignments.Where(static a => a.Tipsy > 0).GroupBy(static a => a.Player))
        {
            if (run.GetPlayer(group.Key.Value)?.Creature is { CombatState: not null } creature)
            {
                await PowerCmd.Apply<SpiritsTipsyPower>(choiceContext ?? new ThrowingPlayerChoiceContext(), creature, group.Sum(static a => a.Tipsy), null, null, silent: true);
            }
        }
    }
}
