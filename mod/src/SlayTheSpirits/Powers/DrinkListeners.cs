using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;

namespace SlayTheSpirits.Powers;

/// <summary>A power that reacts to drinks ("whenever you drink…").</summary>
public interface ISpiritsDrinkListener
{
    /// <summary>
    /// Someone drank (not Water), during combat. <paramref name="mine"/>: it was this power's owner. Runs inside the
    /// drinking game's delivery, in lockstep on every peer.
    /// </summary>
    Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext choiceContext);
}

/// <summary>Tells every <see cref="ISpiritsDrinkListener"/> power about freshly delivered drinks.</summary>
internal static class DrinkListeners
{
    /// <summary>Players in seat order, then their powers in the order they got them, then the drinks in order.</summary>
    public static async Task AfterDrinksAsync(IRunState run, IReadOnlyList<PenaltyAssignment> assignments, PlayerChoiceContext? choiceContext)
    {
        var drinks = assignments.Where(static a => a.Penalty.IsAlcoholic).ToList();
        if (drinks.Count == 0 || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        var context = choiceContext ?? new ThrowingPlayerChoiceContext();
        foreach (var player in run.Players.OrderBy(run.GetPlayerSlotIndex))
        {
            if (player.Creature is not { IsAlive: true, CombatState: not null } creature)
            {
                continue;
            }

            var id = player.NetId;
            foreach (var listener in creature.Powers.OfType<ISpiritsDrinkListener>().ToList())
            {
                foreach (var drink in drinks)
                {
                    await listener.AfterDrinkAsync(drink, drink.Player.Value == id, context);
                }
            }
        }
    }
}
