using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Powers;

namespace SlayTheSpirits.Cards;

// Character-specific Tavern cards, three per character, each leaning on that character's mechanic.
// Registered into the character's pool, so they appear in that character's normal rewards (multiplayer only).

/// <summary>
/// "Down the Hatch" (Ironclad). 2: Take 2 Sips. Deal 8 (12) + half your Tipsy damage. Keeps Keg Stand's id so saved
/// runs load. The Sips land first, so they count toward the damage.
/// </summary>
public sealed class SpiritsKegStand() : SpiritsCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), ISpiritsCharacterCard
{
    private const string Sips = "Sips";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new DynamicVar(Sips, 2m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await GiveSipsAsync(choiceContext, Owner, DynamicVars[Sips].IntValue);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + (OwnerTipsy / 2))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

/// <summary>Silent. 1 (0): The next penalty you'd receive goes to a random teammate. Draw 1 card.</summary>
public sealed class SpiritsSlipAway() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SlayTheSpiritsMod.Game?.AddRedirect(new PlayerId(Owner.NetId));
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>Defect. 1: Take a Sip. Channel 2 (3) Frost.</summary>
public sealed class SpiritsLiquidCooling() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), ISpiritsCharacterCard
{
    private const string Frost = "Frost";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(Frost, 2m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GiveSipsAsync(choiceContext, Owner);
        for (var i = 0; i < DynamicVars[Frost].IntValue; i++)
        {
            await OrbCmd.Channel<FrostOrb>(choiceContext, Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars[Frost].UpgradeValueBy(1m);
}

/// <summary>Regent. 1: Gain 2 (3) Stars. Choose a teammate; they Sip.</summary>
public sealed class SpiritsRoyalDecree() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly), ISpiritsCharacterCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new StarsVar(2)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target?.Player);
        await PlayerCmd.GainStars(DynamicVars.Stars.BaseValue, Owner);
        await GiveSipsAsync(choiceContext, cardPlay.Target.Player);
    }

    protected override void OnUpgrade() => DynamicVars.Stars.UpgradeValueBy(1m);
}

/// <summary>Necrobinder. 0: Only playable if a teammate has died. Summon 6 (9). Everyone Sips.</summary>
public sealed class SpiritsPourOneOut() : SpiritsCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SummonVar(6m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override bool IsPlayable => IsMutable && CombatState?.PlayerCreatures.Any(c => c != Owner.Creature && c.IsDead) == true;

    protected override bool ShouldGlowGoldInternal => IsPlayable;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await OstyCmd.Summon(choiceContext, Owner, DynamicVars["Summon"].BaseValue, this);
        await GiveEveryoneSipsAsync(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars["Summon"].UpgradeValueBy(3m);
}

/// <summary>Fool. 1: A drinking song! ALL players gain 5 (7) Block. Everyone takes a Sip.</summary>
public sealed class SpiritsDrinkingSong() : SpiritsCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies), ISpiritsCharacterCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var creature in CombatState!.GetTeammatesOf(Owner.Creature).Where(static c => c is { IsAlive: true, IsPlayer: true }).ToList())
        {
            await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);
        }

        await GiveEveryoneSipsAsync(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

// --- More character cards: a Common and a stronger card each ---------------------------------------------------------

/// <summary>Ironclad. Common. 1: Deal 8 (11) damage. At 15+ Tipsy, it hits ALL enemies instead.</summary>
public sealed class SpiritsBarFight() : SpiritsCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy), ISpiritsCharacterCard
{
    public const int Rowdy = 15;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move)];

    protected override bool ScalesWithTipsy => true;

    protected override bool ShouldGlowGoldInternal => OwnerTipsy >= Rowdy;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (OwnerTipsy >= Rowdy)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);
        }
        else
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

/// <summary>Ironclad. Rare Power. 2 (1): Whenever you drink, gain 1 Strength.</summary>
public sealed class SpiritsBeerMuscles() : SpiritsCard(2, CardType.Power, CardRarity.Rare, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<SpiritsBeerMusclesPower>(), HoverTipFactory.FromPower<StrengthPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<SpiritsBeerMusclesPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>Silent. Common. 1: Apply 4 (6) Poison, plus 1 for every 8 Tipsy.</summary>
public sealed class SpiritsDregs() : SpiritsCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy), ISpiritsCharacterCard
{
    public const int TipsyPerPoison = 8;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(4m)];

    protected override bool ScalesWithTipsy => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var poison = DynamicVars["PoisonPower"].BaseValue + (OwnerTipsy / TipsyPerPoison);
        await PowerCmd.Apply<PoisonPower>(choiceContext, cardPlay.Target, poison, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["PoisonPower"].UpgradeValueBy(2m);
}

/// <summary>Silent. Rare. 1 (0): Exhaust. This combat, the first penalty you'd receive each turn goes to a random teammate.</summary>
public sealed class SpiritsDineAndDash() : SpiritsCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self), ISpiritsCharacterCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiritsDineAndDashPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SlayTheSpiritsMod.Game?.ArmTurnRedirect(new PlayerId(Owner.NetId));
        await PowerCmd.Apply<SpiritsDineAndDashPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>Defect. Common. 1: Draw 1 (2). Channel 1 Lightning. Drink some Water.</summary>
public sealed class SpiritsElectrolytes() : SpiritsCard(1, CardType.Skill, CardRarity.Common, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Water];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromOrb<LightningOrb>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await OrbCmd.Channel<LightningOrb>(choiceContext, Owner);
        await TakeWaterAsync(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

/// <summary>Defect. Uncommon Power. 1 (0): Whenever you drink, Channel 1 Frost.</summary>
public sealed class SpiritsBrainFreeze() : SpiritsCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<SpiritsBrainFreezePower>(), HoverTipFactory.FromOrb<FrostOrb>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<SpiritsBrainFreezePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>Regent. Common. 1: Forge 5 (8), plus 1 for every 8 Tipsy.</summary>
public sealed class SpiritsRoyalPour() : SpiritsCard(1, CardType.Skill, CardRarity.Common, TargetType.Self), ISpiritsCharacterCard
{
    private const string Forge = "Forge";

    public const int TipsyPerForge = 8;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(Forge, 5m)];

    protected override bool ScalesWithTipsy => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await ForgeCmd.Forge(DynamicVars[Forge].BaseValue + (OwnerTipsy / TipsyPerForge), Owner, this);

    protected override void OnUpgrade() => DynamicVars[Forge].UpgradeValueBy(3m);
}

/// <summary>Regent. Rare. 1: Exhaust. Gain 2 (3) Stars for each living player. Everyone takes a Sip.</summary>
public sealed class SpiritsOpenBar() : SpiritsCard(1, CardType.Skill, CardRarity.Rare, TargetType.AllAllies), ISpiritsCharacterCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new StarsVar(2)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drinkers = CombatState!.Players.Count(static p => p.Creature.IsAlive);
        await PlayerCmd.GainStars(DynamicVars.Stars.BaseValue * drinkers, Owner);
        await GiveEveryoneSipsAsync(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Stars.UpgradeValueBy(1m);
}

/// <summary>Necrobinder. Common. 1: Summon 5 (7). Take a Sip.</summary>
public sealed class SpiritsRaiseGlass() : SpiritsCard(1, CardType.Skill, CardRarity.Common, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SummonVar(5m)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await OstyCmd.Summon(choiceContext, Owner, DynamicVars["Summon"].BaseValue, this);
        await GiveSipsAsync(choiceContext, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["Summon"].UpgradeValueBy(2m);
}

/// <summary>Necrobinder. Uncommon Power. 2 (1): Whenever a teammate drinks, Summon 2.</summary>
public sealed class SpiritsWake() : SpiritsCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self), ISpiritsCharacterCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiritsWakePower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<SpiritsWakePower>(choiceContext, Owner.Creature, SpiritsWakePower.Summon, Owner.Creature, this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

/// <summary>Fool. Common. 1: You and a teammate gain 6 (9) Block. They take a Sip.</summary>
public sealed class SpiritsKaraokeDuet() : SpiritsCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyAlly), ISpiritsCharacterCard
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6m, ValueProp.Move)];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target?.Player);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CreatureCmd.GainBlock(cardPlay.Target, DynamicVars.Block, cardPlay);
        await GiveSipsAsync(choiceContext, cardPlay.Target.Player);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

/// <summary>Fool. Uncommon. 1: Deal 6 (9) damage, plus 1 for every drink anyone has taken this combat.</summary>
public sealed class SpiritsDrunkenBallad() : SpiritsCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), ISpiritsCharacterCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    /// <summary>Everyone's bar tabs this combat (Water doesn't count).</summary>
    private int DrinksThisCombat => CombatState is null
        ? 0
        : CombatState.PlayerCreatures
            .SelectMany(static c => c.Powers.OfType<SpiritsTabPower>())
            .Where(static p => p.Kind != PenaltyKind.Water)
            .Sum(static p => p.Amount);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + DrinksThisCombat)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
