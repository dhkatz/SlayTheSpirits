using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
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
/// Card effects run inside the lockstep action queue, so the drinks they hand out produce identical results (and
/// identical Tipsy changes) on every peer. Always <c>await</c> them before reading Tipsy.
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

    /// <summary>
    /// <see cref="OwnerTipsy"/> of <paramref name="card"/>, for a <see cref="CalculatedVar"/> multiplier (which must
    /// be static, so it can't read the property itself).
    /// </summary>
    protected static int OwnerTipsyOf(CardModel card) => card is SpiritsCard spirits ? spirits.OwnerTipsy : 0;

    private PenaltyCause CardCause => new(Id.ToString(), Title);

    // Combat cards only ever hand out Sips: Finishing your drink, Chugs and Shots belong to the boss fights, rest
    // sites, the Tavern and events, where they stay rare. Stronger cards cost more Sips instead.

    /// <summary>
    /// Hands <paramref name="drinker"/> <paramref name="count"/> Sips, each its own drink card played straight away.
    /// If it's someone else, the owner says a line and the drinker confirms once (per host rules). The Sips form one
    /// batch, so a redirect (Slip Away) passes them all on together. Call this LAST in <c>OnPlay</c>: after another
    /// player's choice, the rest of the play runs on that player's action queue.
    /// </summary>
    protected async Task GiveSipsAsync(PlayerChoiceContext choiceContext, Player drinker, int count = 1)
    {
        var cards = CreateSips(drinker, count, NextBatch());
        if (drinker != Owner)
        {
            Speech.Giver(Owner, drinker);
        }

        if (await DrinkCards.ConfirmAsync(choiceContext, cards[0], drinker))
        {
            foreach (var card in cards)
            {
                await DrinkCards.PlayAsync(choiceContext, card);
            }
        }
    }

    /// <summary>
    /// Everyone takes <paramref name="count"/> Sips: the owner drinks straight away; every other living player
    /// confirms at the same time, then the drinks play in seat order (so results are identical on every peer whoever
    /// answers first).
    /// </summary>
    protected async Task GiveEveryoneSipsAsync(PlayerChoiceContext choiceContext, int count = 1)
    {
        var combat = CombatState!;
        var players = combat.Players.Where(static p => p.Creature.IsAlive).OrderBy(p => Owner.RunState.GetPlayerSlotIndex(p)).ToList();
        var batch = NextBatch();
        var rounds = players.Select(p => (Player: p, Cards: CreateSips(p, count, batch))).ToList();
        Speech.Giver(Owner, null);

        // Choices may be answered in any order; only the confirmations run concurrently.
        var confirmations = rounds.Select(r => r.Player == Owner
            ? Task.FromResult(true)
            : DrinkCards.ConfirmAsync(new BlockingPlayerChoiceContext(), r.Cards[0], r.Player)).ToList();
        var confirmed = await Task.WhenAll(confirmations);

        for (var i = 0; i < rounds.Count; i++)
        {
            if (!confirmed[i])
            {
                continue;
            }

            foreach (var card in rounds[i].Cards)
            {
                await DrinkCards.PlayAsync(choiceContext, card);
            }
        }
    }

    /// <summary>The owner drinks some Water, as a drink card played straight away.</summary>
    protected async Task TakeWaterAsync(PlayerChoiceContext choiceContext) =>
        await DrinkCards.PlayAsync(choiceContext, DrinkCards.Create(CombatState!, Owner, PenaltyKind.Water, CardCause, Owner));

    /// <summary>Shuffles Sips into a player's pile; they auto-play whenever they're drawn.</summary>
    protected async Task ShuffleSipsAsync(Player drinker, int count, PileType pile = PileType.Draw)
    {
        var cards = Enumerable.Range(0, count).Select(_ => DrinkCards.Create(CombatState!, drinker, PenaltyKind.Sip, CardCause, Owner)).ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(cards, pile, Owner, pile == PileType.Draw ? CardPilePosition.Random : CardPilePosition.Bottom);
    }

    private List<SpiritsDrinkCard> CreateSips(Player drinker, int count, string batch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return [.. Enumerable.Range(0, count).Select(_ => DrinkCards.Create(CombatState!, drinker, PenaltyKind.Sip, CardCause, Owner, batch))];
    }

    /// <summary>
    /// A name for the drinks from one play. Only compared with other batch names on the same machine, so it needn't
    /// match across peers; every peer still groups the same drinks together.
    /// </summary>
    private string NextBatch() => $"{Id.Entry}#{Interlocked.Increment(ref _batches)}";

    private static long _batches;
}
