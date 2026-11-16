using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Cards;

namespace SlayTheSpirits.Game;

/// <summary>
/// A pick of Tavern cards (the colorless set plus the player's character's), every card equally likely, for rest site
/// tabs and Tavern events. Follows the Tavern Cards / Character Cards switches like any other source; if they leave
/// nothing, it offers normal cards.
/// </summary>
internal static class TavernCardOffer
{
    public static List<CardCreationResult> For(Player player, int count)
    {
        var tavern = CardCreationOptions.ForNonCombatWithUniformOdds(
            [ModelDb.CardPool<ColorlessCardPool>(), player.Character.CardPool],
            static card => card is SpiritsCard and not SpiritsCurse);
        // Counted the way the game picks (it throws if asked for more cards than it can find).
        var available = tavern.GetPossibleCards(player)
            .Count(c => c.IsAllowedInRun(player.RunState) && c.Rarity is not (CardRarity.Basic or CardRarity.Ancient));
        return available > 0
            ? [.. CardFactory.CreateForReward(player, Math.Min(count, available), tavern)]
            : [.. CardFactory.CreateForReward(player, count, CardCreationOptions.ForRoom(player, RoomType.Elite))];
    }
}
