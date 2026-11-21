using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Relics;

/// <summary>
/// Base for Tavern runes: Hextech runes for ARAM: Mayhem, offered by its act-start rune choice when that mod is
/// installed (registered in <see cref="SlayTheSpiritsMod"/> through <c>context.Hextech</c>). Without it they're unused.
/// </summary>
/// <remarks>
/// <para>
/// Starter rarity, which ARAM requires: no other relic source draws Starter relics at random, so only ARAM's own rune
/// choice hands them out. Like other Tavern content they're for multiplayer runs with the drinking game on, and the
/// host's <see cref="ContentToggles.HextechRunes"/> switch (<see cref="IsOffered"/>, which ARAM asks on every peer).
/// </para>
/// <para>
/// Like any Tavern relic, they hear every delivered drink through <see cref="SpiritsRelic.AfterDrinkAsync"/>, in lockstep.
/// In combat they only ever hand out Sips.
/// </para>
/// </remarks>
public abstract class SpiritsRune : SpiritsRelic
{
    /// <summary>The "Tavern" source label on ARAM's rune choice (<c>HEXTECH_POOL.SPIRITS_TAVERN</c>).</summary>
    public const string PoolLabelKey = "SPIRITS_TAVERN";

    /// <summary>The heading over these runes in ARAM's config menu (a <c>relic_collection</c> key).</summary>
    public const string SectionTitleKey = "SPIRITS_HEXTECH_SECTION";

    public sealed override RelicRarity Rarity => RelicRarity.Starter;

    /// <summary>The owner is alive in a combat that's going on.</summary>
    protected bool OwnerFighting => InCombat && Owner.Creature is { IsAlive: true, CombatState: not null };

    /// <summary>
    /// Whether ARAM may offer Tavern runes to <paramref name="player"/>: a multiplayer run under host rules with the
    /// drinking game and Tavern runes on. Reads only synced state, so every peer agrees.
    /// </summary>
    public static bool IsOffered(Player player) =>
        player.RunState.Players.Count > 1
        && SlayTheSpiritsMod.Instance?.Rules.Effective is { Enabled: true, Content.HextechRunes: true };

    /// <summary><see cref="IsOffered"/>, for a <typeparamref name="TCharacter"/> player only.</summary>
    public static bool IsOfferedTo<TCharacter>(Player player)
        where TCharacter : CharacterModel => player.Character is TCharacter && IsOffered(player);

    protected static PlayerChoiceContext ChoiceOrDefault(PlayerChoiceContext? choiceContext) =>
        choiceContext ?? new ThrowingPlayerChoiceContext();

    /// <summary>Deals unpowered damage from the owner to every enemy that can be hit.</summary>
    protected async Task DamageAllEnemiesAsync(PlayerChoiceContext? choiceContext, int amount)
    {
        if (Owner.Creature.CombatState?.HittableEnemies.ToList() is { Count: > 0 } enemies)
        {
            await CreatureCmd.Damage(ChoiceOrDefault(choiceContext), enemies, amount, ValueProp.Unpowered, Owner.Creature);
        }
    }
}

// --- Silver ----------------------------------------------------------------------------------------------------------

/// <summary>Silver. Whenever you drink during combat, gain 3 Block.</summary>
public sealed class SpiritsChaser : SpiritsRune
{
    public const int Block = 3;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && OwnerFighting)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, Block * drink.Penalty.Count, ValueProp.Unpowered, null);
        }
    }
}

/// <summary>Silver. Whenever a teammate drinks during combat, deal 3 damage to ALL enemies.</summary>
public sealed class SpiritsCheersToThat : SpiritsRune
{
    public const int Damage = 3;

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine && OwnerFighting)
        {
            Flash();
            await DamageAllEnemiesAsync(choiceContext, Damage * drink.Penalty.Count);
        }
    }
}

/// <summary>
/// Silver. The first time you drink each turn during combat, draw 2 cards. "Each turn" runs from the start of one of
/// your turns to the next, so a drink on the enemies' turn uses it up.
/// </summary>
public sealed class SpiritsSocialDrinker : SpiritsRune
{
    public const int Cards = 2;

    private bool _drewThisTurn;

    public override Task BeforeCombatStart()
    {
        _drewThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            _drewThisTurn = false;
        }

        return Task.CompletedTask;
    }

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && OwnerFighting && !_drewThisTurn)
        {
            _drewThisTurn = true;
            Flash();
            await CardPileCmd.Draw(ChoiceOrDefault(choiceContext), Cards, Owner);
        }
    }
}

/// <summary>Silver, Ironclad. Whenever you drink during combat, gain 3 Vigor.</summary>
public sealed class SpiritsBarBrawler : SpiritsRune
{
    public const int Vigor = 3;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && OwnerFighting)
        {
            Flash();
            await PowerCmd.Apply<VigorPower>(ChoiceOrDefault(choiceContext), Owner.Creature, Vigor * drink.Penalty.Count, Owner.Creature, null);
        }
    }
}

/// <summary>Silver, Fool. Whenever you drink during combat, ALL other players gain 4 Block.</summary>
public sealed class SpiritsKaraokeNight : SpiritsRune
{
    public const int Block = 4;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine || !OwnerFighting)
        {
            return;
        }

        var teammates = Owner.Creature.CombatState!.GetTeammatesOf(Owner.Creature)
            .Where(c => c != Owner.Creature && c is { IsAlive: true, IsPlayer: true })
            .ToList();
        if (teammates.Count == 0)
        {
            return;
        }

        Flash();
        foreach (var teammate in teammates)
        {
            await CreatureCmd.GainBlock(teammate, Block * drink.Penalty.Count, ValueProp.Unpowered, null);
        }
    }
}

// --- Gold ------------------------------------------------------------------------------------------------------------

/// <summary>Gold. At the start of each combat, gain 1 Strength and 1 Dexterity, plus 1 of each for every 20 Tipsy (max 4).</summary>
public sealed class SpiritsTolerance : SpiritsRune
{
    public const int TipsyPerStack = 20;

    public const int MaxStacks = 4;

    protected override bool MentionsTipsy => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom || Owner.Creature is not { IsAlive: true } creature)
        {
            return;
        }

        var stacks = Math.Min(1 + (OwnerTipsy / TipsyPerStack), MaxStacks);
        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), creature, stacks, creature, null);
        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), creature, stacks, creature, null);
    }
}

/// <summary>Gold. At the start of your turn, if a teammate drank since your last turn began, gain 1 Energy.</summary>
public sealed class SpiritsPartyAnimal : SpiritsRune
{
    public const int Energy = 1;

    private bool _teammateDrank;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.ForEnergy(this)];

    public override Task BeforeCombatStart()
    {
        _teammateDrank = false;
        return Task.CompletedTask;
    }

    protected override Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine && OwnerFighting)
        {
            _teammateDrank = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !_teammateDrank)
        {
            return;
        }

        _teammateDrank = false;
        Flash();
        await PlayerCmd.GainEnergy(Energy, Owner);
    }
}

/// <summary>Gold. Upon pickup, raise Max HP by 8. Whenever you Finish your drink, Chug or take a Shot, raise Max HP by 3.</summary>
public sealed class SpiritsIronLiver : SpiritsRune
{
    public const int PickupMaxHp = 8;

    public const int MaxHpPerBigDrink = 3;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Finish, PenaltyKind.Chug, PenaltyKind.Shot];

    public override async Task AfterObtained() => await CreatureCmd.GainMaxHp(Owner.Creature, PickupMaxHp);

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && drink.Penalty.Kind >= PenaltyKind.Finish && Owner.Creature.IsAlive)
        {
            Flash();
            await CreatureCmd.GainMaxHp(Owner.Creature, MaxHpPerBigDrink * drink.Penalty.Count);
        }
    }
}

/// <summary>Gold, Silent. Whenever any player drinks during combat, apply 1 Poison to ALL enemies.</summary>
public sealed class SpiritsSpikedDrink : SpiritsRune
{
    public const int Poison = 1;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!OwnerFighting || Owner.Creature.CombatState!.HittableEnemies.ToList() is not { Count: > 0 } enemies)
        {
            return;
        }

        Flash();
        foreach (var enemy in enemies)
        {
            await PowerCmd.Apply<PoisonPower>(ChoiceOrDefault(choiceContext), enemy, Poison * drink.Penalty.Count, Owner.Creature, null);
        }
    }
}

/// <summary>Gold, Defect. Whenever you drink during combat, Channel 1 Lightning.</summary>
public sealed class SpiritsEnergyDrink : SpiritsRune
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Channeling), HoverTipFactory.FromOrb<LightningOrb>()];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine || !OwnerFighting)
        {
            return;
        }

        Flash();
        for (var i = 0; i < drink.Penalty.Count; i++)
        {
            await OrbCmd.Channel<LightningOrb>(ChoiceOrDefault(choiceContext), Owner);
        }
    }
}

/// <summary>Gold, Regent. Whenever a teammate drinks during combat, gain 1 Star.</summary>
public sealed class SpiritsRoyalToast : SpiritsRune
{
    public const int Stars = 1;

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine && OwnerFighting)
        {
            Flash();
            await PlayerCmd.GainStars(Stars * drink.Penalty.Count, Owner);
        }
    }
}

/// <summary>Gold, Necrobinder. Whenever you drink during combat, Summon 3.</summary>
public sealed class SpiritsBottledSpirits : SpiritsRune
{
    public const int Summon = 3;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.SummonStatic)];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (mine && OwnerFighting)
        {
            Flash();
            await OstyCmd.Summon(ChoiceOrDefault(choiceContext), Owner, Summon * drink.Penalty.Count, this);
        }
    }
}

// --- Prismatic -------------------------------------------------------------------------------------------------------

/// <summary>
/// Prismatic. At the start of each elite and boss combat, gain 3 Strength and 3 Dexterity, and every other player
/// takes a Sip.
/// </summary>
/// <remarks>
/// Runs in <c>BeforeCombatStartLate</c>, after the drinking game has put everyone's Tipsy back on (in
/// <c>BeforeCombatStart</c>), so the Sips add to it rather than replacing it.
/// </remarks>
public sealed class SpiritsKingsCup : SpiritsRune
{
    public const int Stats = 3;

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [PenaltyKind.Sip];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];

    public override async Task BeforeCombatStartLate()
    {
        if (Owner.Creature is not { IsAlive: true, CombatState.Encounter.RoomType: RoomType.Elite or RoomType.Boss } creature)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), creature, Stats, creature, null);
        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), creature, Stats, creature, null);
        if (SlayTheSpiritsMod.Game is { } game)
        {
            var me = GameTriggers.Id(Owner);
            var request = new PenaltyRequest(new Penalty(PenaltyKind.Sip), TargetKind.Others, me, me, new PenaltyCause(Id.ToString(), GameTriggers.Text(Title)));
            await game.PenalizeAsync(request, null, CombatPhase.Starting);
        }
    }
}

/// <summary>Prismatic. Whenever you drink during combat, ALL players gain 3 Block and ALL enemies take 5 damage.</summary>
public sealed class SpiritsWaterfall : SpiritsRune
{
    public const int Block = 3;

    public const int Damage = 5;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    protected override async Task AfterDrinkAsync(PenaltyAssignment drink, bool mine, PlayerChoiceContext? choiceContext)
    {
        if (!mine || !OwnerFighting)
        {
            return;
        }

        Flash();
        var count = drink.Penalty.Count;
        foreach (var player in Owner.Creature.CombatState!.PlayerCreatures.Where(static c => c.IsAlive).ToList())
        {
            await CreatureCmd.GainBlock(player, Block * count, ValueProp.Unpowered, null);
        }

        await DamageAllEnemiesAsync(choiceContext, Damage * count);
    }
}
