using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Cards;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Relics;
using SpireKit.Content;

namespace SlayTheSpirits.Game;

/// <summary>
/// A voluntary heavy drink at a rest site, taken instead of Rest or Smith for a permanent reward. Shown while the
/// rules offer rest site drinks (<see cref="RestSiteDrinks.IsOffered"/>); greyed out once the player has had it
/// this act under the "once per act" limit (<see cref="RestSiteDrinks.IsAvailable"/>).
/// </summary>
/// <remarks>
/// <para>
/// The game runs a rest site choice on every peer, and card and relic picks are synced by the game, so the reward
/// and the drink are recorded identically everywhere. The reward comes first: backing out of it uses nothing up and
/// costs no drink.
/// </para>
/// <para>
/// The offer is generated once per visit and kept, so backing out and picking the option again shows the same
/// cards or relics instead of a fresh roll.
/// </para>
/// </remarks>
internal abstract class SpiritsRestSiteDrink(Player owner, PenaltyKind kind) : SpireRestSiteOption(owner)
{
    public const string Table = "rest_site_ui";

    public PenaltyKind Kind => kind;

    public override string IconPath => $"res://SlayTheSpirits/images/rest_site/{OptionId.ToLowerInvariant()}.png";

    public override bool IsEnabled => SlayTheSpiritsMod.Game?.IsRestSiteDrinkAvailable(Player, kind) ?? false;

    /// <summary>Offers <paramref name="kind"/> to <paramref name="player"/> if the rules in effect allow it.</summary>
    public static RestSiteOption? For(Player player, PenaltyKind kind) =>
        SlayTheSpiritsMod.Game is { IsRestSiteDrinkOffered: true, IsMultiplayerRun: true }
            ? kind switch
            {
                PenaltyKind.Chug => new SpiritsChugRestSiteOption(player),
                PenaltyKind.Shot => new SpiritsShotRestSiteOption(player),
                _ => null,
            }
            : null;

    public override async Task<bool> OnSelect()
    {
        if (!await GrantRewardAsync())
        {
            return false;
        }

        if (SlayTheSpiritsMod.Game is { } game)
        {
            await game.TakeRestSiteDrinkAsync(Player, kind, new PenaltyCause($"restsite.{kind.ToString().ToLowerInvariant()}", "Rest site"));
        }

        return true;
    }

    /// <summary>Gives the reward; false if the player backed out.</summary>
    protected abstract Task<bool> GrantRewardAsync();
}

/// <summary>
/// Chug: choose 2 of 5 Tavern cards (the colorless set plus your character's), every card equally likely. Follows the
/// Tavern Cards / Character Cards switches like any other source; if they leave nothing, it offers normal cards.
/// </summary>
internal sealed class SpiritsChugRestSiteOption(Player owner) : SpiritsRestSiteDrink(owner, PenaltyKind.Chug)
{
    public const string Id = "SPIRITS_CHUG";
    public const int Offered = 5;
    public const int Picked = 2;

    private List<CardCreationResult>? _offer;

    public override string OptionId => Id;

    protected override async Task<bool> GrantRewardAsync()
    {
        _offer ??= Offer();
        var prefs = new CardSelectorPrefs(new LocString(Table, $"{Id}.selectionScreenPrompt"), Math.Min(Picked, _offer.Count))
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };

        var picked = (await CardSelectCmd.FromSimpleGridForRewards(new BlockingPlayerChoiceContext(), _offer, Player, prefs)).ToList();
        if (picked.Count == 0)
        {
            return false;
        }

        foreach (var card in picked)
        {
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
        }

        return true;
    }

    private List<CardCreationResult> Offer()
    {
        var tavern = CardCreationOptions.ForNonCombatWithUniformOdds(
            [ModelDb.CardPool<ColorlessCardPool>(), Player.Character.CardPool],
            static card => card is SpiritsCard);
        // Counted the way the game picks (it throws if asked for more cards than it can find).
        var available = tavern.GetPossibleCards(Player)
            .Count(c => c.IsAllowedInRun(Player.RunState) && c.Rarity is not (CardRarity.Basic or CardRarity.Ancient));
        return available > 0
            ? [.. CardFactory.CreateForReward(Player, Math.Min(Offered, available), tavern)]
            : [.. CardFactory.CreateForReward(Player, Offered, CardCreationOptions.ForRoom(Player, RoomType.Elite))];
    }
}

/// <summary>
/// Shot: choose 1 of up to <see cref="Choices"/> Tavern relics the player doesn't have yet (they only come from here).
/// Once they have them all, it gives the next relic from the normal reward pool instead.
/// </summary>
internal sealed class SpiritsShotRestSiteOption(Player owner) : SpiritsRestSiteDrink(owner, PenaltyKind.Shot)
{
    public const string Id = "SPIRITS_SHOT";
    public const int Choices = 3;

    private List<RelicModel>? _offer;

    public override string OptionId => Id;

    protected override async Task<bool> GrantRewardAsync()
    {
        _offer ??= Offer(Player);
        return await ChooseAsync(Player, _offer);
    }

    /// <summary>Lets <paramref name="player"/> pick one of <paramref name="offer"/>; false if they backed out.</summary>
    internal static async Task<bool> ChooseAsync(Player player, IReadOnlyList<RelicModel> offer)
    {
        var chosen = offer.Count == 1 ? offer[0] : await RelicSelectCmd.FromChooseARelicScreen(player, offer);
        if (chosen is null)
        {
            return false;
        }

        await RelicCmd.Obtain(chosen.ToMutable(), player);
        return true;
    }

    /// <summary>Up to <see cref="Choices"/> Tavern relics <paramref name="player"/> doesn't have (also the Tavern's Top Shelf).</summary>
    internal static List<RelicModel> Offer(Player player)
    {
        var rng = player.PlayerRng.Rewards;
        var unowned = SpiritsRelic.All.Where(r => !player.Relics.Any(owned => owned.Id == r.Id)).ToList();
        var offer = new List<RelicModel>();
        while (offer.Count < Choices && unowned.Count > 0)
        {
            var pick = rng.NextItem(unowned)!;
            unowned.Remove(pick);
            offer.Add(pick);
        }

        return offer.Count > 0 ? offer : [RelicFactory.PullNextRelicForRewardFromFront(player)];
    }
}
