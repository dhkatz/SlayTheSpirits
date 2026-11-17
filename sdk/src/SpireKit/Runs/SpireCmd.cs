using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;

namespace SpireKit.Runs;

/// <summary>
/// Gives players cards, relics and potions. Each method takes either kind of model, canonical or a player's copy,
/// and passes the game the one it wants.
/// </summary>
/// <remarks>
/// <para>
/// The game uses one type for both: the canonical model (<c>ModelDb.Card&lt;T&gt;()</c>, what card pools and
/// lists of ids hold) and a player's copy of it (<c>ToMutable()</c>). Its commands each want one or the other and
/// throw on the wrong one (<c>MutableModelException</c> or <c>CanonicalModelException</c>), and nothing in their
/// signatures says which: <c>CardPileCmd.AddCursesToDeck</c> wants canonical cards and makes the copies itself,
/// while <c>CardPileCmd.Add</c>, <c>RelicCmd.Obtain</c> and <c>PotionCmd.TryToProcure</c> want copies.
/// </para>
/// <para>
/// The game's own commands are banned in this repo's builds (<c>build/BannedSymbols.txt</c>), so code goes through
/// these instead.
/// </para>
/// </remarks>
#pragma warning disable RS0030 // The one place that calls the game's commands.
public static class SpireCmd
{
    /// <summary>Adds curses to <paramref name="player"/>'s deck, with the game's preview.</summary>
    /// <remarks>A copy that was passed in becomes a fresh copy of its curse.</remarks>
    public static async Task<IReadOnlyList<CardPileAddResult>> AddCursesToDeck(Player player, IEnumerable<CardModel> curses)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(curses);
        return [.. await CardPileCmd.AddCursesToDeck(curses.Select(static c => c.CanonicalInstance).ToList(), player)];
    }

    /// <summary>Adds a curse to <paramref name="player"/>'s deck, with the game's preview.</summary>
    public static async Task<CardPileAddResult> AddCurseToDeck(Player player, CardModel curse) =>
        (await AddCursesToDeck(player, [curse]))[0];

    /// <summary>Adds a card to <paramref name="player"/>'s deck. Shows nothing: pass the result to <c>CardCmd.PreviewCardPileAdd</c>.</summary>
    /// <remarks>
    /// A canonical card becomes a new copy for the player. A copy from the game's card factories or a card reward is
    /// used as it is, upgrades and all.
    /// </remarks>
    public static Task<CardPileAddResult> AddToDeck(Player player, CardModel card)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(card);
        return CardPileCmd.Add(Owned(player, card), PileType.Deck);
    }

    /// <summary>Gives <paramref name="player"/> a relic: a canonical one becomes a new copy.</summary>
    public static Task<RelicModel> ObtainRelic(Player player, RelicModel relic)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(relic);
        return RelicCmd.Obtain(relic.IsMutable ? relic : relic.ToMutable(), player);
    }

    /// <summary>Gives <paramref name="player"/> a potion if they have room: a canonical one becomes a new copy.</summary>
    public static Task<PotionProcureResult> TryToProcurePotion(Player player, PotionModel potion)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(potion);
        return PotionCmd.TryToProcure(potion.IsMutable ? potion : potion.ToMutable(), player);
    }

    /// <summary>A copy of <paramref name="card"/> that belongs to <paramref name="player"/> and the run.</summary>
    private static CardModel Owned(Player player, CardModel card)
    {
        if (card.IsCanonical)
        {
            return player.RunState.CreateCard(card, player);
        }

        // A bare ToMutable() copy hasn't been added to the run yet (the game's factories do that).
        if (card.Owner is null)
        {
            player.RunState.AddCard(card, player);
            card.AfterCreated();
        }

        return card;
    }
}
#pragma warning restore RS0030
