using Godot;
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
using SlayTheSpirits.Relics;
using SpireKit.Content;
using SpireKit.Runs;

namespace SlayTheSpirits.Game;

/// <summary>
/// A rest site tab, taken instead of Rest or Smith: a permanent reward now, paid for with a random Tavern curse in your
/// deck (<see cref="SpiritsCurse"/>). Shown while the rules offer tabs (<see cref="RestSiteTabs.IsOffered"/>); greyed
/// out once the player has had it this act under the "once per act" limit (<see cref="RestSiteTabs.IsAvailable"/>).
/// </summary>
/// <remarks>
/// <para>
/// The game runs a rest site choice on every peer, and card and relic picks are synced by the game, so the reward
/// and the curse (rolled on the player's reward RNG) are identical everywhere. The reward comes first: backing out of
/// it uses nothing up and costs no curse. Gaining the curse fires the usual "gains a curse" rule.
/// </para>
/// <para>
/// The offer is generated once per visit and kept, so backing out and picking the option again shows the same
/// cards or relics instead of a fresh roll.
/// </para>
/// </remarks>
internal abstract class SpiritsRestSiteTab(Player owner, RestSiteTab tab) : SpireRestSiteOption(owner)
{
    public const string Table = "rest_site_ui";

    /// <summary>
    /// Developer flag: rest sites offer tabs in solo runs too, so they can be tried without a second player (the dev
    /// console's <c>room restsite</c> opens one). Solo runs have no Tavern cards, so Open a Tab offers normal ones
    /// (<see cref="TavernCardOffer"/>). <c>tools/launch.ps1 -Tabs</c>.
    /// </summary>
    public const string DevFlag = "--spirits-tabs";

    private static readonly bool Forced = OS.GetCmdlineArgs().Contains(DevFlag);

    public RestSiteTab Tab => tab;

    public override string IconPath => $"res://SlayTheSpirits/images/rest_site/{OptionId.ToLowerInvariant()}.png";

    public override bool IsEnabled => SlayTheSpiritsMod.Game?.IsRestSiteTabAvailable(Player, tab) ?? false;

    /// <summary>The curses this tab can cost.</summary>
    protected abstract IReadOnlyList<CardModel> Curses { get; }

    /// <summary>Offers <paramref name="tab"/> to <paramref name="player"/> if the rules in effect allow it.</summary>
    public static RestSiteOption? For(Player player, RestSiteTab tab) =>
        SlayTheSpiritsMod.Game is { IsRestSiteTabOffered: true } game && (game.IsMultiplayerRun || Forced)
            ? tab switch
            {
                RestSiteTab.Open => new SpiritsOpenTabRestSiteOption(player),
                RestSiteTab.RunUp => new SpiritsRunUpTabRestSiteOption(player),
                _ => null,
            }
            : null;

    protected override async Task<bool> SelectAsync()
    {
        if (!await GrantRewardAsync())
        {
            return false;
        }

        SlayTheSpiritsMod.Game?.TakeRestSiteTab(Player, tab);
        if (Player.PlayerRng.Rewards.NextItem(Curses) is { } curse)
        {
            await SpireCmd.AddCurseToDeck(Player, curse);
        }

        return true;
    }

    /// <summary>Gives the reward; false if the player backed out.</summary>
    protected abstract Task<bool> GrantRewardAsync();
}

/// <summary>Open a Tab: choose 2 of 5 Tavern cards (<see cref="TavernCardOffer"/>), for a minor Tavern curse.</summary>
/// <remarks>Keeps the old Chug option's id, which saved run histories refer to (and its icon).</remarks>
internal sealed class SpiritsOpenTabRestSiteOption(Player owner) : SpiritsRestSiteTab(owner, RestSiteTab.Open)
{
    public const string Id = "SPIRITS_CHUG";
    public const int Offered = 5;
    public const int Picked = 2;

    private List<CardCreationResult>? _offer;

    public override string OptionId => Id;

    protected override IReadOnlyList<CardModel> Curses => SpiritsCurse.Minor;

    protected override async Task<bool> GrantRewardAsync()
    {
        _offer ??= TavernCardOffer.For(Player, Offered);
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
            CardCmd.PreviewCardPileAdd(await SpireCmd.AddToDeck(Player, card));
        }

        return true;
    }

}

/// <summary>
/// Run Up the Tab: choose 1 of up to <see cref="Choices"/> Tavern relics the player doesn't have yet, for a major
/// Tavern curse. Once they have them all, it gives the next relic from the normal reward pool instead.
/// </summary>
/// <remarks>Keeps the old Shot option's id, which saved run histories refer to (and its icon).</remarks>
internal sealed class SpiritsRunUpTabRestSiteOption(Player owner) : SpiritsRestSiteTab(owner, RestSiteTab.RunUp)
{
    public const string Id = "SPIRITS_SHOT";
    public const int Choices = 3;

    private List<RelicModel>? _offer;

    public override string OptionId => Id;

    protected override IReadOnlyList<CardModel> Curses => SpiritsCurse.Major;

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

        await SpireCmd.ObtainRelic(player, chosen);
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
