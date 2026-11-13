using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Powers;
using SlayTheSpirits.Presentation;
using SlayTheSpirits.Rules;
using SpireKit.Content;

namespace SlayTheSpirits.Cards;

/// <summary>Marks a Tavern card that belongs to a character pool (toggled by <see cref="ContentToggles.CharacterCards"/>).</summary>
public interface ISpiritsCharacterCard;

/// <summary>
/// Base for Tavern cards: multiplayer-only, able to hand out penalties, and aware of Tipsy.
/// </summary>
/// <remarks>
/// Card effects run inside the lockstep action queue, so <see cref="PenalizeAsync"/> produces identical
/// results (and identical Tipsy changes) on every peer. Always <c>await</c> it before reading Tipsy.
/// </remarks>
public abstract class SpiritsCard(int energyCost, CardType type, CardRarity rarity, TargetType targetType)
    : SpireCard(energyCost, type, rarity, targetType)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    /// <summary>Penalty keywords in this card's text; each gets a tooltip, followed by Tipsy.</summary>
    protected virtual IEnumerable<PenaltyKind> PenaltyKeywords => [];

    /// <summary>True for cards whose effect scales with Tipsy (adds the Tipsy tooltip even without penalties).</summary>
    protected virtual bool ScalesWithTipsy => false;

    /// <summary>Tooltips beyond the generated keyword ones.</summary>
    protected virtual IEnumerable<IHoverTip> AdditionalHoverTips => [];

    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = SpiritsTips.ForPenalties(PenaltyKeywords).ToList();
            if (ScalesWithTipsy && tips.Count == 0)
            {
                tips.Add(SpiritsTips.Tipsy());
            }

            return [.. tips, .. AdditionalHoverTips];
        }
    }

    /// <summary>The card owner's current Tipsy (the buff amount; 0 outside combat or on library cards).</summary>
    protected int OwnerTipsy => IsMutable && CombatState is not null ? Owner.Creature.GetPowerAmount<SpiritsTipsyPower>() : 0;

    private PenaltyCause CardCause => new(Id.ToString(), Title);

    /// <summary>
    /// Hands <paramref name="drinker"/> a drink card. If it's someone else, the owner says a line and the drinker
    /// confirms it on the card-selection screen (per host rules), then it auto-plays. Call this LAST in
    /// <c>OnPlay</c>: after another player's choice, the rest of the play runs on that player's action queue.
    /// </summary>
    protected async Task GiveDrinkAsync(PlayerChoiceContext choiceContext, Player drinker, PenaltyKind kind)
    {
        var card = DrinkCards.Create(CombatState!, drinker, kind, CardCause, Owner);
        if (drinker != Owner)
        {
            Speech.Giver(Owner, drinker);
        }

        if (await DrinkCards.ConfirmAsync(choiceContext, card, drinker))
        {
            await DrinkCards.PlayAsync(choiceContext, card);
        }
    }

    /// <summary>
    /// Everyone drinks: the owner drinks straight away; every other living player confirms at the same time,
    /// then the drinks play in seat order (so results are identical on every peer whoever answers first).
    /// </summary>
    protected async Task GiveEveryoneAsync(PlayerChoiceContext choiceContext, PenaltyKind kind)
    {
        var combat = CombatState!;
        var players = combat.Players.Where(static p => p.Creature.IsAlive).OrderBy(p => Owner.RunState.GetPlayerSlotIndex(p)).ToList();
        var cards = players.Select(p => (Player: p, Card: DrinkCards.Create(combat, p, kind, CardCause, Owner))).ToList();
        Speech.Giver(Owner, null);

        // Choices may be answered in any order; only the confirmations run concurrently.
        var confirmations = cards.Select(c => c.Player == Owner
            ? Task.FromResult(true)
            : DrinkCards.ConfirmAsync(new BlockingPlayerChoiceContext(), c.Card, c.Player)).ToList();
        var confirmed = await Task.WhenAll(confirmations);

        for (var i = 0; i < cards.Count; i++)
        {
            if (confirmed[i])
            {
                await DrinkCards.PlayAsync(choiceContext, cards[i].Card);
            }
        }
    }

    /// <summary>Shuffles drink cards into a player's pile; they auto-play whenever they're drawn.</summary>
    protected async Task ShuffleDrinksAsync(Player drinker, PenaltyKind kind, int count, PileType pile = PileType.Draw)
    {
        var cards = Enumerable.Range(0, count).Select(_ => DrinkCards.Create(CombatState!, drinker, kind, CardCause, Owner)).ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(cards, pile, Owner, pile == PileType.Draw ? CardPilePosition.Random : CardPilePosition.Bottom);
    }

    /// <summary>Hands out a penalty caused by this card and updates Tipsy (no drink card; e.g. curses).</summary>
    /// <param name="choiceContext">The card play's context.</param>
    /// <param name="penalty">What to drink.</param>
    /// <param name="target">Who, relative to <paramref name="subject"/>.</param>
    /// <param name="subject">The chosen player; defaults to the card's owner.</param>
    protected Task PenalizeAsync(PlayerChoiceContext choiceContext, Penalty penalty, TargetKind target, Player? subject = null) =>
        SlayTheSpiritsMod.Game?.PenalizeAsync(
            new PenaltyRequest(
                penalty,
                target,
                new PlayerId((subject ?? Owner).NetId),
                new PlayerId(Owner.NetId),
                CardCause),
            choiceContext) ?? Task.CompletedTask;
}
