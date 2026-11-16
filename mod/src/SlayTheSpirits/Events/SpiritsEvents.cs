using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SpireKit.Content;

namespace SlayTheSpirits.Events;

/// <summary>
/// Base for Tavern events: "?" rooms in any act, in multiplayer runs while the host's rules have the drinking game and
/// Tavern Events on. Each player gets their own copy (seeded per player), and the game runs every player's choices
/// on every peer, so the drinks they choose go through the drinking game in lockstep.
/// </summary>
/// <remarks>
/// Drinks taken here are chosen for a reward, so they're voluntary (<see cref="PenaltyRequest.Voluntary"/>): never
/// redirected, skipped or capped. Finishes, Chugs and Shots belong out of combat, so events may hand them out.
/// </remarks>
public abstract class SpiritsEvent : SpireEvent
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.Count > 1
        && SlayTheSpiritsMod.Instance?.Rules.Effective is { Enabled: true, Content.TavernEvents: true };

    /// <summary>The owner drinks <paramref name="penalty"/>, by choice.</summary>
    protected Task DrinkAsync(Penalty penalty) =>
        SlayTheSpiritsMod.Game?.DrinkAsync(Owner!, penalty, new PenaltyCause($"event.{Id.Entry.ToLowerInvariant()}", GameTriggers.Text(Title)), null)
        ?? Task.CompletedTask;

    /// <summary>Ends the event on the page <paramref name="page"/> (its <c>.pages.&lt;PAGE&gt;.description</c> text).</summary>
    protected void FinishOn(string page) => SetEventFinished(L10NLookup($"{Id.Entry}.pages.{page}.description"));

    /// <summary>Lets the owner add 1 of <paramref name="offered"/> Tavern cards to their deck.</summary>
    protected async Task ChooseTavernCardAsync(int offered, string page)
    {
        var prefs = new CardSelectorPrefs(L10NLookup($"{Id.Entry}.pages.{page}.selectionScreenPrompt"), 1) { Cancelable = false };
        await SelectCardsToAddToDeckFromGrid(TavernCardOffer.For(Owner!, offered), prefs);
    }

    /// <summary>Lets the owner upgrade up to <paramref name="count"/> cards in their deck.</summary>
    protected async Task UpgradeCardsAsync(int count)
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, count) { Cancelable = false };
        foreach (var card in (await CardSelectCmd.FromDeckForUpgrade(Owner!, prefs)).ToList())
        {
            CardCmd.Upgrade(card);
        }
    }

    protected bool HasUpgradableCards => PileType.Deck.GetPile(Owner!).Cards.Any(static c => c?.IsUpgradable ?? false);
}

/// <summary>
/// The Speakeasy: a door in an alley, a slot at eye level. "Password?" Know it (Finish your drink: a Tavern card and an
/// upgrade), bluff (a coin flip between getting in free and the bouncer), or walk away.
/// </summary>
public sealed class SpiritsSpeakeasy : SpiritsEvent
{
    private const string BouncerHpLoss = "BouncerHpLoss";

    public const int BouncerPercent = 15;

    public const int Offered = 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(BouncerHpLoss, 10m, ValueProp.Unblockable | ValueProp.Unpowered)];

    public override void CalculateVars() =>
        DynamicVars[BouncerHpLoss].BaseValue = Math.Max(1, Owner!.Creature.MaxHp * BouncerPercent / 100);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, Swordfish, InitialOptionKey("SWORDFISH")),
        new EventOption(this, Bluff, InitialOptionKey("BLUFF")).ThatDoesDamage(DynamicVars[BouncerHpLoss].BaseValue),
        new EventOption(this, Leave, InitialOptionKey("LEAVE")),
    ];

    private async Task Swordfish()
    {
        await ChooseTavernCardAsync(Offered, "SWORDFISH");
        if (HasUpgradableCards)
        {
            await UpgradeCardsAsync(1);
        }

        await DrinkAsync(Penalty.Of(PenaltyKind.Finish));
        FinishOn("SWORDFISH");
    }

    private async Task Bluff()
    {
        if (Rng.NextInt(2) == 0)
        {
            await ChooseTavernCardAsync(Offered, "BLUFF_IN");
            FinishOn("BLUFF_IN");
            return;
        }

        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, (DamageVar)DynamicVars[BouncerHpLoss], null, null, null);
        FinishOn("BLUFF_OUT");
    }

    private Task Leave()
    {
        FinishOn("LEAVE");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Seven Grand: a whiskey bar with a wall of bottles and pool tables in the back. The bartender's pick (a Shot for a
/// Rare relic), a whiskey flight (3 Sips to upgrade 2 cards), a game of pool for gold (loser drinks), or leave.
/// </summary>
public sealed class SpiritsSevenGrand : SpiritsEvent
{
    public const int Flight = 3;

    public const int FlightUpgrades = 2;

    public const int Bet = 50;

    public const int Winnings = 100;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, BartendersPick, InitialOptionKey("BARTENDERS_PICK")),
        new EventOption(this, HasUpgradableCards ? WhiskeyFlight : null, InitialOptionKey("FLIGHT")),
        new EventOption(this, Owner!.Gold >= Bet ? RackEmUp : null, InitialOptionKey("POOL")),
        new EventOption(this, Leave, InitialOptionKey("LEAVE")),
    ];

    private async Task BartendersPick()
    {
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner!, RelicRarity.Rare).ToMutable(), Owner!);
        await DrinkAsync(Penalty.Of(PenaltyKind.Shot));
        FinishOn("BARTENDERS_PICK");
    }

    private async Task WhiskeyFlight()
    {
        await UpgradeCardsAsync(FlightUpgrades);
        await DrinkAsync(Penalty.Of(PenaltyKind.Sip, Flight));
        FinishOn("FLIGHT");
    }

    private async Task RackEmUp()
    {
        if (Rng.NextInt(2) == 0)
        {
            await PlayerCmd.GainGold(Winnings, Owner!);
            FinishOn("POOL_WIN");
            return;
        }

        await PlayerCmd.LoseGold(Bet, Owner!, GoldLossType.Lost);
        await DrinkAsync(Penalty.Of(PenaltyKind.Sip));
        FinishOn("POOL_LOSE");
    }

    private Task Leave()
    {
        FinishOn("LEAVE");
        return Task.CompletedTask;
    }
}
