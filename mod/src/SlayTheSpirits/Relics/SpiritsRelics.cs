using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Presentation;
using SpireKit.Content;

namespace SlayTheSpirits.Relics;

/// <summary>
/// Base for Tavern relics: Event rarity, so they never drop on their own. The only way to get one is the Shot at a
/// rest site (<see cref="SpiritsShotRestSiteOption"/>), which makes them the payoff for the night's rarest drink.
/// </summary>
/// <remarks>
/// <see cref="AfterDrinkAsync"/> is called for every drink anyone takes, from inside the drinking game's delivery,
/// which runs in lockstep on every peer (game hooks and card plays), so relic effects stay in sync.
/// </remarks>
public abstract class SpiritsRelic : SpireRelic
{
    /// <summary>Every Tavern relic, canonical, in a fixed order (the Shot's offer draws from this).</summary>
    public static IReadOnlyList<RelicModel> All =>
    [
        ModelDb.Relic<SpiritsHipFlask>(),
        ModelDb.Relic<SpiritsBeerBelly>(),
        ModelDb.Relic<SpiritsTipJar>(),
        ModelDb.Relic<SpiritsDrinkingBuddy>(),
        ModelDb.Relic<SpiritsPintGlass>(),
    ];

    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>The owner's run-long Tipsy.</summary>
    protected int OwnerTipsy => SlayTheSpiritsMod.Game?.TipsyOf(Owner) ?? 0;

    protected static bool InCombat => CombatManager.Instance.IsInProgress;

    /// <summary>Penalty keywords in this relic's text; each gets a tooltip.</summary>
    protected virtual IEnumerable<PenaltyKind> PenaltyKeywords => [];

    /// <summary>True for relics that read Tipsy.</summary>
    protected virtual bool MentionsTipsy => false;

    /// <summary>Tooltips beyond the generated drink ones.</summary>
    protected virtual IEnumerable<IHoverTip> AdditionalHoverTips => [];

    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = SpiritsTips.ForPenalties(PenaltyKeywords).ToList();
            if (MentionsTipsy && tips.Count == 0)
            {
                tips.Add(SpiritsTips.Tipsy());
            }

            return [.. tips, .. AdditionalHoverTips];
        }
    }

    /// <summary>Someone drank (not Water). <paramref name="mine"/>: it was this relic's owner.</summary>
    protected virtual Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext) => Task.CompletedTask;

    /// <summary>Tells every Tavern relic about freshly delivered drinks, players in seat order.</summary>
    internal static async Task AfterDrinksAsync(IRunState run, IReadOnlyList<PenaltyAssignment> assignments, PlayerChoiceContext? choiceContext)
    {
        var drinks = assignments.Where(static a => a.Penalty.IsAlcoholic).ToList();
        if (drinks.Count == 0)
        {
            return;
        }

        foreach (var player in run.Players.OrderBy(run.GetPlayerSlotIndex))
        {
            var id = GameTriggers.Id(player);
            foreach (var relic in player.Relics.OfType<SpiritsRelic>().ToList())
            {
                foreach (var drink in drinks)
                {
                    await relic.AfterDrinkAsync(drink, drink.Player == id, choiceContext);
                }
            }
        }
    }
}

/// <summary>At the start of each combat, gain 1 Strength for every 15 Tipsy.</summary>
public sealed class SpiritsHipFlask : SpiritsRelic
{
    public const int TipsyPerStrength = 15;

    protected override bool MentionsTipsy => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        var strength = OwnerTipsy / TipsyPerStrength;
        if (room is CombatRoom && strength > 0)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, strength, Owner.Creature, null);
        }
    }
}

/// <summary>Upon pickup, raise Max HP by 8. At the start of each combat, gain Block equal to a quarter of your Tipsy (max 15).</summary>
public sealed class SpiritsBeerBelly : SpiritsRelic
{
    public const int MaxHp = 8;

    public const int TipsyPerBlock = 4;

    public const int MaxBlock = 15;

    public override bool HasUponPickupEffect => true;

    protected override bool MentionsTipsy => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterObtained() => await CreatureCmd.GainMaxHp(Owner.Creature, MaxHp);

    public override async Task BeforeCombatStart()
    {
        var block = Math.Min(OwnerTipsy / TipsyPerBlock, MaxBlock);
        if (block > 0)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, null);
        }
    }
}

/// <summary>Whenever you drink, gain 2 Gold for each Tipsy it gives.</summary>
public sealed class SpiritsTipJar : SpiritsRelic
{
    public const int GoldPerTipsy = 2;

    protected override bool MentionsTipsy => true;

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && drink.Tipsy > 0)
        {
            Flash();
            await PlayerCmd.GainGold(GoldPerTipsy * drink.Tipsy, Owner);
        }
    }
}

/// <summary>Whenever a teammate drinks during combat, gain 3 Block.</summary>
public sealed class SpiritsDrinkingBuddy : SpiritsRelic
{
    public const int Block = 3;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine && InCombat && Owner.Creature is { IsAlive: true, CombatState: not null } creature)
        {
            Flash();
            await CreatureCmd.GainBlock(creature, Block * drink.Penalty.Count, ValueProp.Unpowered, null);
        }
    }
}

/// <summary>Whenever you Finish your drink, take a Chug or take a Shot, heal 6 HP.</summary>
public sealed class SpiritsPintGlass : SpiritsRelic
{
    public const int Heal = 6;

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Finish, PenaltyKind.Chug, PenaltyKind.Shot];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && drink.Penalty.Kind >= PenaltyKind.Finish && Owner.Creature.IsAlive)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, Heal * drink.Penalty.Count);
        }
    }
}
