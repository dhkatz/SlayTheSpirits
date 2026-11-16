using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Powers;

namespace SlayTheSpirits.Cards;

// Colorless, multiplayer-only Tavern cards. Class names carry the mod prefix because the game derives
// model ids (and localization keys) from the class name alone: SpiritsCheers -> SPIRITS_CHEERS.
//
// Convention: do the card owner's own effects first and hand drinks to OTHER players last. After another
// player's confirmation, the rest of the play continues on that player's action queue.

/// <summary>0: Draw 1 (2). Choose a teammate; they Sip. Exhaust.</summary>
public sealed class SpiritsCheers() : SpiritsCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target?.Player);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await GiveSipsAsync(choiceContext, cardPlay.Target.Player);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>1: Draw 2 (3). Choose a teammate; they Sip.</summary>
public sealed class SpiritsPeerPressure() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target?.Player);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await GiveSipsAsync(choiceContext, cardPlay.Target.Player);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>
/// 0: Draw 1 (2) card. Take a Sip. Draws 1 more each time it's played this combat (max 4). Then it goes into a random
/// teammate's draw pile, like the game's The Ball: whoever holds the bottle drinks from it.
/// </summary>
/// <remarks>
/// The game moves it (and hands it to the new owner) through <see cref="GetResultLocationForCardPlay"/>, the same way
/// as The Ball. Its draw count lives on the combat copy, so it grows as it goes round the table and the deck copy
/// starts fresh next combat.
/// </remarks>
public sealed class SpiritsPassTheBottle() : SpiritsCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public const int MaxCards = 4;

    private decimal _extraCards;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    /// <summary>Draws added by plays this combat; kept so a downgrade (e.g. Magiknight) doesn't lose them.</summary>
    private decimal ExtraCards
    {
        get => _extraCards;
        set
        {
            AssertMutable();
            _extraCards = value;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        if (DynamicVars.Cards.BaseValue < MaxCards)
        {
            DynamicVars.Cards.BaseValue += 1;
            ExtraCards += 1;
        }

        await GiveSipsAsync(choiceContext, Owner);
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        var location = base.GetResultLocationForCardPlay();
        if (CombatState is null)
        {
            return location;
        }

        var teammates = CombatState.GetTeammatesOf(Owner.Creature)
            .Where(c => c is { IsAlive: true, IsPlayer: true } && c.Player != Owner)
            .ToList();
        if (Owner.RunState.Rng.CombatTargets.NextItem(teammates)?.Player is not { } teammate)
        {
            return location;
        }

        location.player = teammate;
        if (location.pileType == PileType.Discard)
        {
            location.pileType = PileType.Draw;
            location.position = CardPilePosition.Random;
        }

        return location;
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        DynamicVars.Cards.BaseValue += ExtraCards;
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>0: Shuffle 2 Sips into your draw pile. Gain 1 (2) Energy.</summary>
public sealed class SpiritsPregame() : SpiritsCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShuffleSipsAsync(Owner, 2);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1m);
}

/// <summary>1: Take a Sip. Gain 2 (3) Strength.</summary>
public sealed class SpiritsLiquidCourage() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(2m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GiveSipsAsync(choiceContext, Owner);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Strength.UpgradeValueBy(1m);
}

/// <summary>1: Heal HP equal to your Tipsy (max 10 (15)). Exhaust.</summary>
public sealed class SpiritsHairOfTheDog() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const string MaxHeal = "MaxHeal";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(MaxHeal, 10m)];

    protected override bool ScalesWithTipsy => true;

    protected override bool IsPlayable => OwnerTipsy > 0;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var amount = Math.Min(OwnerTipsy, DynamicVars[MaxHeal].IntValue);
        if (amount > 0)
        {
            await CreatureCmd.Heal(Owner.Creature, amount);
        }
    }

    protected override void OnUpgrade() => DynamicVars[MaxHeal].UpgradeValueBy(5m);
}

/// <summary>2: All players gain 8 (11) Block. Everyone Drinks.</summary>
public sealed class SpiritsRoundOnMe() : SpiritsCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var creature in CombatState!.GetTeammatesOf(Owner.Creature).Where(static c => c is { IsAlive: true, IsPlayer: true }).ToList())
        {
            await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);
        }

        await GiveEveryoneSipsAsync(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>1 (0): You're immune to penalties this combat (water still counts). Exhaust.</summary>
public sealed class SpiritsDesignatedDriver() : SpiritsCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiritsDesignatedDriverPower>()];

    /// <summary>Not while you're the Drunk Driver.</summary>
    protected override bool IsPlayable => !IsMutable || CombatState is null || !Owner.Creature.HasPower<SpiritsDrunkDriverPower>();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SlayTheSpiritsMod.Game?.GrantImmunity(new PlayerId(Owner.NetId));
        await PowerCmd.Apply<SpiritsDesignatedDriverPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>2 (1): Draw 3 cards. Gain 1 Energy. Choose a teammate; they take 2 Sips. Exhaust.</summary>
public sealed class SpiritsBottomsUp() : SpiritsCard(2, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
{
    private const string Sips = "Sips";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3), new EnergyVar(1), new DynamicVar(Sips, 2m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target?.Player);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await GiveSipsAsync(choiceContext, cardPlay.Target.Player, DynamicVars[Sips].IntValue);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>5 (4): Retain. Exhaust. All players gain 3 Strength. Everyone takes 2 Sips.</summary>
public sealed class SpiritsLastCall() : SpiritsCard(5, CardType.Skill, CardRarity.Rare, TargetType.AllAllies)
{
    private const string Sips = "Sips";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(3m), new DynamicVar(Sips, 2m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var creature in CombatState!.GetTeammatesOf(Owner.Creature).Where(static c => c is { IsAlive: true, IsPlayer: true }).ToList())
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, creature, DynamicVars.Strength.BaseValue, Owner.Creature, this);
        }

        await GiveEveryoneSipsAsync(choiceContext, DynamicVars[Sips].IntValue);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>
/// "Take the Shot". 3 (2): Retain. Exhaust. Take 3 Sips. Deal damage equal to 2x your Tipsy to ALL enemies.
/// </summary>
/// <remarks>
/// Retired: no longer in any card pool, since nobody takes a Shot voluntarily more than once an act and a card
/// would just sit in the deck. Shots now come from rest sites instead. Still registered (every model in the DLL
/// is) so runs saved with it load; it keeps Double Shot's id for the same reason. Like every combat card it only
/// hands out Sips now.
/// </remarks>
public sealed class SpiritsDoubleShot() : SpiritsCard(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    private const string Multiplier = "Multiplier";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(Multiplier, 2m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GiveSipsAsync(choiceContext, Owner, 3);
        var damage = OwnerTipsy * DynamicVars[Multiplier].IntValue;
        if (damage > 0)
        {
            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>
/// Power. 1: At the start of your turn, deal damage equal to a quarter of your Tipsy (up to 12 (16)) to a random enemy.
/// Can't be played while you're the Designated Driver (and the other way round).
/// </summary>
public sealed class SpiritsDrunkDriver() : SpiritsCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    private const string MaxDamage = "MaxDamage";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(MaxDamage, 12m)];

    protected override bool ScalesWithTipsy => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiritsDrunkDriverPower>()];

    protected override bool IsPlayable => !IsMutable || CombatState is null || !Owner.Creature.HasPower<SpiritsDesignatedDriverPower>();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<SpiritsDrunkDriverPower>(choiceContext, Owner.Creature, DynamicVars[MaxDamage].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars[MaxDamage].UpgradeValueBy(4m);
}

/// <summary>
/// X: Take X Sips. Deal 4 (6) damage to ALL enemies X times, +1 per hit for every 15 Tipsy. The Sips land first, so
/// they count toward the bonus.
/// </summary>
public sealed class SpiritsBottomlessMimosas() : SpiritsCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public const int TipsyPerDamage = 15;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var refills = ResolveEnergyXValue();
        if (refills <= 0)
        {
            return;
        }

        await GiveSipsAsync(choiceContext, Owner, refills);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + (OwnerTipsy / TipsyPerDamage))
            .WithHitCount(refills)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

/// <summary>
/// Rare. 3 (2): Exhaust. Take 3 Sips. Then play the top card of your draw pile once for every 8 Tipsy (max 8),
/// Exhausting them, and end your turn. You don't remember any of it.
/// </summary>
public sealed class SpiritsBlackout() : SpiritsCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const string Sips = "Sips";

    public const int TipsyPerCard = 8;

    public const int MaxCards = 8;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(Sips, 3m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GiveSipsAsync(choiceContext, Owner, DynamicVars[Sips].IntValue);
        var cards = Math.Min(OwnerTipsy / TipsyPerCard, MaxCards);
        if (cards > 0)
        {
            await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, cards, CardPilePosition.Top, forceExhaust: true);
        }

        // The game ends a player's turn the same way when they die on it.
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
