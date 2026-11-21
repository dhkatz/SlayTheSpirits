using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using SlayTheSpirits.Cards;
using SlayTheSpirits.Events;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Potions;
using SlayTheSpirits.Presentation;
using SlayTheSpirits.Relics;
using SlayTheSpirits.Rules;
using SpireKit.Compat;
using SpireKit.Modding;

namespace SlayTheSpirits;

/// <summary>Composition root: builds the mod's services and registers its content.</summary>
public sealed class SlayTheSpiritsMod : SpireMod
{
    public override string Id => "SlayTheSpirits";

    public override string ModelPrefix => "Spirits";

    /// <summary>
    /// The booted mod, for model classes the game instantiates itself (cards, powers) and therefore can't
    /// receive dependencies. Everything else gets services through constructors.
    /// </summary>
    internal static SlayTheSpiritsMod? Instance { get; private set; }

    /// <summary>The running drinking game (see <see cref="Instance"/>).</summary>
    internal static DrinkGame? Game => Instance?.DrinkGame;

    internal RulesService Rules { get; private set; } = null!;

    internal DrinkGame DrinkGame { get; private set; } = null!;

    protected override void OnInitialize(ModContext context)
    {
        Instance = this;
        Rules = new RulesService(context);
        DrinkGame = new DrinkGame(context, Rules, new Announcer(context.Toasts, () => Rules.Effective));
        Rules.Initialize();
        RulesSettingsTab.Register(context, Rules);
        foreach (var tab in Engine.RestSiteTabs.All)
        {
            context.RestSite.Add(player => SpiritsRestSiteTab.For(player, tab));
        }

        context.Content
            // Colorless Tavern set: shops, colorless rewards, card generators.
            .AddCard<ColorlessCardPool, SpiritsCheers>()
            .AddCard<ColorlessCardPool, SpiritsPeerPressure>()
            .AddCard<ColorlessCardPool, SpiritsPassTheBottle>()
            .AddCard<ColorlessCardPool, SpiritsPregame>()
            .AddCard<ColorlessCardPool, SpiritsLiquidCourage>()
            .AddCard<ColorlessCardPool, SpiritsHairOfTheDog>()
            .AddCard<ColorlessCardPool, SpiritsRoundOnMe>()
            .AddCard<ColorlessCardPool, SpiritsDesignatedDriver>()
            .AddCard<ColorlessCardPool, SpiritsBottomsUp>()
            .AddCard<ColorlessCardPool, SpiritsLastCall>()
            .AddCard<ColorlessCardPool, SpiritsDrunkDriver>()
            .AddCard<ColorlessCardPool, SpiritsBottomlessMimosas>()
            .AddCard<ColorlessCardPool, SpiritsBlackout>()
            .AddCard<ColorlessCardPool, SpiritsTakeOneForTheTeam>()
            .AddCard<ColorlessCardPool, SpiritsWaterBreak>()
            .AddCard<ColorlessCardPool, SpiritsHappyHour>()
            // Drinks as status cards: handed out by Tavern cards, auto-played on confirm or draw.
            .AddCard<StatusCardPool, SpiritsWater>()
            .AddCard<StatusCardPool, SpiritsSip>()
            .AddCard<StatusCardPool, SpiritsShot>()
            .AddCard<StatusCardPool, SpiritsChug>()
            .AddCard<StatusCardPool, SpiritsFinishYourDrink>()
            // Tavern curses, the price of a rest site tab. The minor ones turn up wherever random curses do; the
            // major ones are filtered out of random sources (RulesService.IsCardAllowed).
            .AddCard<CurseCardPool, SpiritsSpill>()
            .AddCard<CurseCardPool, SpiritsDrunkText>()
            .AddCard<CurseCardPool, SpiritsHiccups>()
            .AddCard<CurseCardPool, SpiritsBarFly>()
            .AddCard<CurseCardPool, SpiritsStickyFloor>()
            .AddCard<CurseCardPool, SpiritsHangover>()
            .AddCard<CurseCardPool, SpiritsBeerGoggles>()
            .AddCard<CurseCardPool, SpiritsFuzzyMemory>()
            // Three per character, in that character's normal rewards.
            .AddCard<IroncladCardPool, SpiritsKegStand>()
            .AddCard<IroncladCardPool, SpiritsBarFight>()
            .AddCard<IroncladCardPool, SpiritsBeerMuscles>()
            .AddCard<SilentCardPool, SpiritsSlipAway>()
            .AddCard<SilentCardPool, SpiritsDregs>()
            .AddCard<SilentCardPool, SpiritsDineAndDash>()
            .AddCard<DefectCardPool, SpiritsLiquidCooling>()
            .AddCard<DefectCardPool, SpiritsElectrolytes>()
            .AddCard<DefectCardPool, SpiritsBrainFreeze>()
            .AddCard<RegentCardPool, SpiritsRoyalDecree>()
            .AddCard<RegentCardPool, SpiritsRoyalPour>()
            .AddCard<RegentCardPool, SpiritsOpenBar>()
            .AddCard<NecrobinderCardPool, SpiritsPourOneOut>()
            .AddCard<NecrobinderCardPool, SpiritsRaiseGlass>()
            .AddCard<NecrobinderCardPool, SpiritsWake>()
            .AddCard<FoolCardPool, SpiritsDrinkingSong>()
            .AddCard<FoolCardPool, SpiritsKaraokeDuet>()
            .AddCard<FoolCardPool, SpiritsDrunkenBallad>()
            // Tavern relics never drop: running up a rest site tab and the Tavern's Top Shelf are the only ways to get one.
            .AddRelic<EventRelicPool, SpiritsHipFlask>()
            .AddRelic<EventRelicPool, SpiritsBeerBelly>()
            .AddRelic<EventRelicPool, SpiritsTipJar>()
            .AddRelic<EventRelicPool, SpiritsDrinkingBuddy>()
            .AddRelic<EventRelicPool, SpiritsPintGlass>()
            .AddRelic<EventRelicPool, SpiritsJungleJuice>()
            // Tavern potions drop like any other in multiplayer runs.
            .AddPotion<SharedPotionPool, SpiritsLightBeer>()
            .AddPotion<SharedPotionPool, SpiritsTallboy>()
            .AddPotion<SharedPotionPool, SpiritsFirewater>()
            // Tavern events join the shared "?" rooms; they only allow themselves in multiplayer runs.
            .AddEvent<SpiritsSpeakeasy>()
            .AddEvent<SpiritsSevenGrand>()
            .SetCardFilter(Rules.IsCardAllowed)
            .SetPotionFilter(potion => DrinkGame.IsMultiplayerRun && Rules.IsPotionAllowed(potion));

        // Tavern runes for ARAM: Mayhem, only if it's installed: its act-start rune choice offers them in multiplayer
        // runs with the drinking game and Tavern runes on. Generic ones say "Tavern"; character ones keep the
        // character's label, and are only offered to that character.
        context.Hextech
            .SetSectionTitle(SpiritsRune.SectionTitleKey)
            .AddRune<SpiritsChaser>(HextechRarity.Silver, "SURVIVAL", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsCheersToThat>(HextechRarity.Silver, "MULTIPLAYER", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsSocialDrinker>(HextechRarity.Silver, "DRAW", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsTolerance>(HextechRarity.Gold, "STACKING", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsPartyAnimal>(HextechRarity.Gold, "RESOURCE", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsIronLiver>(HextechRarity.Gold, "SURVIVAL", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsKingsCup>(HextechRarity.Prismatic, "MULTIPLAYER", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsWaterfall>(HextechRarity.Prismatic, "MULTIPLAYER", poolLabel: SpiritsRune.PoolLabelKey, isAvailable: SpiritsRune.IsOffered)
            .AddRune<SpiritsBarBrawler>(HextechRarity.Silver, "OUTPUT", HextechCharacter.Ironclad, isAvailable: SpiritsRune.IsOfferedTo<Ironclad>)
            .AddRune<SpiritsKaraokeNight>(HextechRarity.Silver, "MULTIPLAYER", HextechCharacter.Fool, isAvailable: SpiritsRune.IsOfferedTo<Fool>)
            .AddRune<SpiritsSpikedDrink>(HextechRarity.Gold, "POISON", HextechCharacter.Silent, isAvailable: SpiritsRune.IsOfferedTo<Silent>)
            .AddRune<SpiritsEnergyDrink>(HextechRarity.Gold, "ORB", HextechCharacter.Defect, isAvailable: SpiritsRune.IsOfferedTo<Defect>)
            .AddRune<SpiritsRoyalToast>(HextechRarity.Gold, "STARLIGHT", HextechCharacter.Regent, isAvailable: SpiritsRune.IsOfferedTo<Regent>)
            .AddRune<SpiritsBottledSpirits>(HextechRarity.Gold, "SUMMON", HextechCharacter.Necrobinder, isAvailable: SpiritsRune.IsOfferedTo<Necrobinder>);
    }

    protected override void OnGameReady(ModContext context)
    {
        TabText.Apply(context.Loc, Rules.Effective);
        RestSiteText.Apply(context.Loc);
        Rules.EffectiveChanged += rules => TabText.Apply(context.Loc, rules);
        Rules.LoadProfile();
    }
}
