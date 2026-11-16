using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using SlayTheSpirits.Cards;
using SlayTheSpirits.Game;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Potions;
using SlayTheSpirits.Presentation;
using SlayTheSpirits.Relics;
using SlayTheSpirits.Rules;
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
            // Drinks as status cards: handed out by Tavern cards, auto-played on confirm or draw.
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
            // One per character, in that character's normal rewards.
            .AddCard<IroncladCardPool, SpiritsKegStand>()
            .AddCard<SilentCardPool, SpiritsSlipAway>()
            .AddCard<DefectCardPool, SpiritsLiquidCooling>()
            .AddCard<RegentCardPool, SpiritsRoyalDecree>()
            .AddCard<NecrobinderCardPool, SpiritsPourOneOut>()
            .AddCard<FoolCardPool, SpiritsDrinkingSong>()
            // Tavern relics never drop: running up a rest site tab and the Tavern's Top Shelf are the only ways to get one.
            .AddRelic<EventRelicPool, SpiritsHipFlask>()
            .AddRelic<EventRelicPool, SpiritsBeerBelly>()
            .AddRelic<EventRelicPool, SpiritsTipJar>()
            .AddRelic<EventRelicPool, SpiritsDrinkingBuddy>()
            .AddRelic<EventRelicPool, SpiritsPintGlass>()
            // Tavern potions drop like any other in multiplayer runs.
            .AddPotion<SharedPotionPool, SpiritsLightBeer>()
            .AddPotion<SharedPotionPool, SpiritsTallboy>()
            .AddPotion<SharedPotionPool, SpiritsFirewater>()
            .SetCardFilter(Rules.IsCardAllowed)
            .SetPotionFilter(potion => DrinkGame.IsMultiplayerRun && Rules.IsPotionAllowed(potion));
    }

    protected override void OnGameReady(ModContext context)
    {
        TabText.Apply(context.Loc, Rules.Effective);
        RestSiteText.Apply(context.Loc);
        Rules.EffectiveChanged += rules => TabText.Apply(context.Loc, rules);
        Rules.LoadProfile();
    }
}
