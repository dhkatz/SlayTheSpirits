using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Cards;

// Tavern curses: what a rest site tab costs (Game/RestSiteTabOptions.cs). Minor curses also turn up wherever random
// curses do in multiplayer; major curses only come from running up a tab. The Tavern's Settle Up removes any of them.

/// <summary>A Tavern curse that never comes from random curse sources, only from running up a rest site tab.</summary>
public interface ISpiritsMajorCurse;

/// <summary>Base for Tavern curses: unplayable, multiplayer-only, and able to hand out Sips like any Tavern card.</summary>
public abstract class SpiritsCurse() : SpiritsCard(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
{
    /// <summary>The minor curses, canonical, in a fixed order (Open a Tab rolls from these).</summary>
    public static IReadOnlyList<CardModel> Minor =>
    [
        ModelDb.Card<SpiritsSpill>(),
        ModelDb.Card<SpiritsDrunkText>(),
        ModelDb.Card<SpiritsHiccups>(),
        ModelDb.Card<SpiritsBarFly>(),
        ModelDb.Card<SpiritsStickyFloor>(),
    ];

    /// <summary>The major curses, canonical, in a fixed order (Run Up the Tab rolls from these).</summary>
    public static IReadOnlyList<CardModel> Major =>
    [
        ModelDb.Card<SpiritsHangover>(),
        ModelDb.Card<SpiritsBeerGoggles>(),
        ModelDb.Card<SpiritsFuzzyMemory>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public sealed override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
        {
            await WhenDrawnAsync(choiceContext);
        }
    }

    /// <summary>This curse was drawn.</summary>
    protected virtual Task WhenDrawnAsync(PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>Whether this curse is in its owner's hand right now.</summary>
    protected bool InHand => Pile?.Type == PileType.Hand;

    /// <summary>The owner's living teammates, in seat order.</summary>
    protected List<Player> Teammates() =>
        [.. CombatState!.Players.Where(p => p != Owner && p.Creature.IsAlive).OrderBy(p => Owner.RunState.GetPlayerSlotIndex(p))];
}

/// <summary>Minor. When drawn, take a Sip. (Also turns up wherever random curses do, in multiplayer.)</summary>
public sealed class SpiritsSpill : SpiritsCurse
{
    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override Task WhenDrawnAsync(PlayerChoiceContext choiceContext) => GiveSipsAsync(choiceContext, Owner);
}

/// <summary>Minor. When drawn, a random teammate takes a Sip.</summary>
public sealed class SpiritsDrunkText : SpiritsCurse
{
    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task WhenDrawnAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner.RunState.Rng.CombatTargets.NextItem(Teammates()) is { } teammate)
        {
            await GiveSipsAsync(choiceContext, teammate);
        }
    }
}

/// <summary>Minor. While in your hand, every 3rd card you play makes you take a Sip.</summary>
public sealed class SpiritsHiccups : SpiritsCurse
{
    public const int Every = 3;

    private int _played;

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override Task WhenDrawnAsync(PlayerChoiceContext choiceContext)
    {
        _played = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // The Sips it hands out are cards played too; they don't count.
        if (!InHand || cardPlay.Card.Owner != Owner || cardPlay.Card is SpiritsDrinkCard)
        {
            return;
        }

        _played++;
        if (_played % Every == 0)
        {
            await GiveSipsAsync(choiceContext, Owner);
        }
    }
}

/// <summary>Minor. When drawn, take a Sip, then it flies off into a random teammate's draw pile.</summary>
public sealed class SpiritsBarFly : SpiritsCurse
{
    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task WhenDrawnAsync(PlayerChoiceContext choiceContext)
    {
        await GiveSipsAsync(choiceContext, Owner);
        if (Owner.RunState.Rng.CombatTargets.NextItem(Teammates()) is { } teammate)
        {
            await CardPileCmd.GiveToAnotherPlayer(this, teammate, PileType.Draw, CardPilePosition.Random);
        }
    }
}

/// <summary>Minor. Unplayable. Retain. It's stuck to your hand.</summary>
public sealed class SpiritsStickyFloor : SpiritsCurse
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable, CardKeyword.Retain];
}

/// <summary>Major. Innate. When drawn, lose 1 Energy and take a Sip.</summary>
public sealed class SpiritsHangover : SpiritsCurse, ISpiritsMajorCurse
{
    public const int Energy = 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable, CardKeyword.Innate];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task WhenDrawnAsync(PlayerChoiceContext choiceContext)
    {
        await PlayerCmd.LoseEnergy(Energy, Owner);
        await GiveSipsAsync(choiceContext, Owner);
    }
}

/// <summary>Major. When drawn, take a Sip. While in your hand, your attacks hit random enemies.</summary>
/// <remarks>
/// The game only asks for the damage target while dealing damage (never for previews), and every peer deals the same
/// damage in the same order, so drawing from the combat targets RNG here stays in lockstep.
/// </remarks>
public sealed class SpiritsBeerGoggles : SpiritsCurse, ISpiritsMajorCurse
{
    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override Task WhenDrawnAsync(PlayerChoiceContext choiceContext) => GiveSipsAsync(choiceContext, Owner);

    public override Creature? ModifyPreBlockDamageTarget(Creature originalTarget, Creature? target, decimal amount, ValueProp props, Creature? dealer)
    {
        if (target is null || !InHand || dealer != Owner.Creature || !props.HasFlag(ValueProp.Move) || target.IsPlayer)
        {
            return target;
        }

        var enemies = CombatState!.HittableEnemies.ToList();
        return enemies.Count > 1 ? Owner.RunState.Rng.CombatTargets.NextItem(enemies) ?? target : target;
    }
}

/// <summary>Major. When drawn, take a Sip and Exhaust a random card in your hand.</summary>
public sealed class SpiritsFuzzyMemory : SpiritsCurse, ISpiritsMajorCurse
{
    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    protected override async Task WhenDrawnAsync(PlayerChoiceContext choiceContext)
    {
        await GiveSipsAsync(choiceContext, Owner);
        var hand = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this).ToList();
        if (Owner.RunState.Rng.CombatCardSelection.NextItem(hand) is { } forgotten)
        {
            await CardCmd.Exhaust(choiceContext, forgotten);
        }
    }
}
