using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Presentation;
using SpireKit.Content;

namespace SlayTheSpirits.Potions;

/// <summary>
/// Base for Tavern potions: drinking one means drinking for real. The drink is voluntary (the player chose to use
/// it), so caps never downgrade it and the "uses a potion" rule doesn't add a second drink on top.
/// </summary>
/// <remarks>
/// They drop like other potions in multiplayer runs while <see cref="Rules.ContentToggles.TavernPotions"/> is on
/// (<see cref="Rules.RulesService.IsPotionAllowed"/>). The drink is taken before the effect, inside the potion's
/// action, so its Tipsy is already counted.
/// </remarks>
public abstract class SpiritsPotion(PenaltyKind drink) : SpirePotion
{
    public PenaltyKind Drink { get; } = drink;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    /// <summary>Tooltips beyond the drink one.</summary>
    protected virtual IEnumerable<IHoverTip> AdditionalHoverTips => [];

    public sealed override IEnumerable<IHoverTip> ExtraHoverTips => [.. SpiritsTips.ForPenalties([Drink]), .. AdditionalHoverTips];

    protected sealed override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (SlayTheSpiritsMod.Game is { } game)
        {
            await game.DrinkAsync(Owner, Drink, new PenaltyCause(Id.ToString(), GameTriggers.Text(Title)), choiceContext);
        }

        await AfterDrinkAsync(choiceContext, target);
    }

    /// <summary>The potion's effect, after its drink.</summary>
    protected abstract Task AfterDrinkAsync(PlayerChoiceContext choiceContext, Creature? target);
}

/// <summary>Common. Take a Sip. Gain 2 Strength and 2 Dexterity.</summary>
public sealed class SpiritsLightBeer() : SpiritsPotion(PenaltyKind.Sip)
{
    public override PotionRarity Rarity => PotionRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(2m), new PowerVar<DexterityPower>(2m)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];

    protected override async Task AfterDrinkAsync(PlayerChoiceContext choiceContext, Creature? target)
    {
        var me = Owner.Creature;
        await PowerCmd.Apply<StrengthPower>(choiceContext, me, DynamicVars["StrengthPower"].BaseValue, me, null);
        await PowerCmd.Apply<DexterityPower>(choiceContext, me, DynamicVars["DexterityPower"].BaseValue, me, null);
    }
}

/// <summary>Uncommon. Take a Chug. Gain 15 Block and 3 Strength.</summary>
public sealed class SpiritsTallboy() : SpiritsPotion(PenaltyKind.Chug)
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(15m, ValueProp.Unpowered), new PowerVar<StrengthPower>(3m)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Block), HoverTipFactory.FromPower<StrengthPower>()];

    protected override async Task AfterDrinkAsync(PlayerChoiceContext choiceContext, Creature? target)
    {
        var me = Owner.Creature;
        await CreatureCmd.GainBlock(me, DynamicVars.Block, null);
        await PowerCmd.Apply<StrengthPower>(choiceContext, me, DynamicVars["StrengthPower"].BaseValue, me, null);
    }
}

/// <summary>Rare. Take a Shot. Deal 25 damage to ALL enemies. Gain 2 Energy.</summary>
public sealed class SpiritsFirewater() : SpiritsPotion(PenaltyKind.Shot)
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(25m, ValueProp.Unpowered), new EnergyVar(2)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.ForEnergy(this)];

    protected override async Task AfterDrinkAsync(PlayerChoiceContext choiceContext, Creature? target)
    {
        var me = Owner.Creature;
        await CreatureCmd.Damage(choiceContext, me.CombatState!.HittableEnemies, DynamicVars.Damage.BaseValue, DynamicVars.Damage.Props, me);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }
}
