using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Tavern;

/// <summary>
/// One item on the Tavern's menu, as an entry in the game's shop inventory. Purchases go through the game's
/// <c>MerchantSynchronizer</c>, which runs them on every peer, so gold, drinks and rewards stay in step; prices go
/// through the game's shop price hook, so shop discounts apply.
/// </summary>
/// <remarks>
/// Order of a purchase: the reward first (a selection screen can be backed out of, which costs nothing), then the
/// gold, then the drink.
/// </remarks>
internal abstract class TavernEntry : MerchantEntry
{
    protected TavernEntry(Player player, TavernItem item)
        : base(player)
    {
        Item = item;
        CalcCost();
    }

    public TavernItem Item { get; }

    /// <summary>Bought this visit (one of each per visit).</summary>
    public bool Bought { get; private set; }

    /// <summary>Whether the item can do anything for this player right now (e.g. a card to upgrade).</summary>
    public virtual bool IsAvailable => true;

    public override bool IsStocked => !Bought && IsAvailable;

    public override void CalcCost() => _cost = TavernMenu.GoldFor(Item, _player.RunState.Players.Count);

    /// <summary>Gives the item's effect; false if the player backed out.</summary>
    protected abstract Task<bool> GrantAsync();

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        if (Bought || !await GrantAsync())
        {
            return (false, 0);
        }

        var gold = ignoreCost ? 0 : Cost;
        if (gold > 0)
        {
            await PlayerCmd.LoseGold(gold, _player, GoldLossType.Spent);
        }

        if (Item.Drink is { } kind && SlayTheSpiritsMod.Game is { } game)
        {
            await game.DrinkAsync(_player, kind, Cause, null);
        }

        return (true, gold);
    }

    protected PenaltyCause Cause => new($"tavern.{Item.Id}", Item.Name);

    protected override void ClearAfterPurchase() => Bought = true;

    // The Courier and friends restock shop items; the Tavern pours one of each per visit regardless.
    protected override void RestockAfterPurchase(MerchantInventory? inventory) => Bought = true;

    /// <summary>The full menu for <paramref name="player"/>, in menu order.</summary>
    public static List<TavernEntry> MenuFor(Player player) =>
    [
        new HouseLagerEntry(player),
        new TheSpecialEntry(player),
        new TopShelfEntry(player),
        new GlassOfWaterEntry(player),
        new BuyARoundEntry(player),
        new TheBouncerEntry(player),
    ];
}

internal sealed class HouseLagerEntry(Player player) : TavernEntry(player, TavernMenu.HouseLager)
{
    protected override async Task<bool> GrantAsync()
    {
        await CreatureCmd.Heal(_player.Creature, TavernMenu.LagerHeal);
        return true;
    }
}

internal sealed class TheSpecialEntry(Player player) : TavernEntry(player, TavernMenu.TheSpecial)
{
    public override bool IsAvailable => Upgradable().Any();

    protected override Task<bool> GrantAsync()
    {
        // Same pick as the game's "random upgrades" shop entry: a stable shuffle on the player's shop RNG.
        var picked = Upgradable().ToList().StableShuffle(_player.PlayerRng.Shops).Take(TavernMenu.SpecialUpgrades).ToList();
        foreach (var card in picked)
        {
            CardCmd.Upgrade(card);
        }

        return Task.FromResult(picked.Count > 0);
    }

    private IEnumerable<CardModel> Upgradable() => PileType.Deck.GetPile(_player).Cards.Where(static c => c?.IsUpgradable ?? false);
}

internal sealed class TopShelfEntry(Player player) : TavernEntry(player, TavernMenu.TopShelf)
{
    private List<RelicModel>? _offer;

    protected override async Task<bool> GrantAsync()
    {
        // Rolled once and kept, so backing out and coming back shows the same bottles.
        _offer ??= SpiritsShotRestSiteOption.Offer(_player);
        return await SpiritsShotRestSiteOption.ChooseAsync(_player, _offer);
    }
}

internal sealed class GlassOfWaterEntry(Player player) : TavernEntry(player, TavernMenu.GlassOfWater)
{
    protected override async Task<bool> GrantAsync()
    {
        await CreatureCmd.Heal(_player.Creature, TavernMenu.WaterHeal);
        return true;
    }
}

internal sealed class BuyARoundEntry(Player player) : TavernEntry(player, TavernMenu.BuyARound)
{
    public override bool IsAvailable => _player.RunState.Players.Count > 1;

    protected override async Task<bool> GrantAsync()
    {
        foreach (var player in _player.RunState.Players.Where(static p => p.Creature.IsAlive))
        {
            await CreatureCmd.Heal(player.Creature, TavernMenu.RoundHeal);
        }

        if (SlayTheSpiritsMod.Game is { } game)
        {
            var me = GameTriggers.Id(_player);
            await game.PenalizeAsync(new PenaltyRequest(new Penalty(PenaltyKind.Sip), TargetKind.Others, me, me, Cause), null);
        }

        return true;
    }
}

/// <summary>Card removal, priced like the game's: it gets dearer each time you use it (here or at a shop).</summary>
internal sealed class TheBouncerEntry(Player player) : TavernEntry(player, TavernMenu.TheBouncer)
{
    public override bool IsAvailable => Hook.ShouldAllowMerchantCardRemoval(_player.RunState, _player);

    public override void CalcCost() =>
        _cost = AscensionHelper.GetValueIfAscension(AscensionLevel.Inflation, TavernMenu.TheBouncer.Gold + 25, TavernMenu.TheBouncer.Gold)
            + (MerchantCardRemovalEntry.PriceIncrease * _player.ExtraFields.CardShopRemovalsUsed);

    protected override Task<bool> GrantAsync() => MerchantCardRemovalEntry.DoCardRemoval(_player, isCancelable: true);
}
