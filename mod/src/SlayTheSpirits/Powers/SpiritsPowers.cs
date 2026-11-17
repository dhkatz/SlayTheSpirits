using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Presentation;
using SpireKit.Content;

namespace SlayTheSpirits.Powers;

/// <summary>
/// Tipsy: the run-long drunkenness counter, shown as a buff under each player's health bar. It has no effect
/// on its own; Tavern cards scale with it.
/// </summary>
/// <remarks>
/// Powers are wiped when combat ends, so the drinking game re-applies each player's running total at the
/// start of every fight and adds to it as penalties land. It is real combat state (checked by the game's
/// desync detection), which is safe because every penalty is computed identically on every peer.
/// </remarks>
public sealed class SpiritsTipsyPower : SpirePower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>Dead players are still at the table.</summary>
    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    /// <summary>Adds "tonight so far" (3 Sips, 1 Shot…) when hovering a player.</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsMutable && Owner.Player is { } player && SpiritsTips.Breakdown(new PlayerId(player.NetId)) is { } tip
            ? [tip]
            : [];
}

/// <summary>Marks a player as immune to penalties for the rest of combat (water still counts).</summary>
public sealed class SpiritsDesignatedDriverPower : SpirePower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>
/// Drunk Driver: at the start of its owner's turn, deals a quarter of their Tipsy (up to <c>Amount</c>) to a random
/// enemy. Turn starts run in lockstep, so the combat targets RNG picks the same enemy on every peer.
/// </summary>
public sealed class SpiritsDrunkDriverPower : SpirePower
{
    public const int TipsyPerDamage = 4;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Owner.CombatState is not { } combat)
        {
            return;
        }

        var damage = Math.Min(Owner.GetPowerAmount<SpiritsTipsyPower>() / TipsyPerDamage, Amount);
        var enemies = combat.HittableEnemies.ToList();
        if (damage <= 0 || player.RunState.Rng.CombatTargets.NextItem(enemies) is not { } target)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(choiceContext, target, damage, ValueProp.Unpowered, Owner, null, null);
    }
}

/// <summary>
/// The bar tab: how many of one drink this player has been handed this combat ("Shots 1", "Sips 2"), so the
/// table can check everyone has kept up. One power per drink kind, so each shows as its own counter.
/// </summary>
/// <remarks>
/// Purely informational and wiped at combat end like every power. Titles and descriptions follow the host's
/// labels; <see cref="Presentation.TabText"/> injects them at runtime.
/// </remarks>
public abstract class SpiritsTabPower(PenaltyKind kind) : SpirePower
{
    public PenaltyKind Kind { get; } = kind;

    /// <summary>A buff, not a debuff: Artifact must never block it.</summary>
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}

public sealed class SpiritsWaterTabPower() : SpiritsTabPower(PenaltyKind.Water);

public sealed class SpiritsSipTabPower() : SpiritsTabPower(PenaltyKind.Sip);


public sealed class SpiritsShotTabPower() : SpiritsTabPower(PenaltyKind.Shot);

public sealed class SpiritsChugTabPower() : SpiritsTabPower(PenaltyKind.Chug);

public sealed class SpiritsFinishTabPower() : SpiritsTabPower(PenaltyKind.Finish);
