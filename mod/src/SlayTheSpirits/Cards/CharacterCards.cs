using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Cards;

// Character-specific Tavern cards, one per character, each leaning on that character's mechanic.
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
