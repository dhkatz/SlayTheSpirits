using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheSpirits.Cards;
using SlayTheSpirits.Game;
using SlayTheSpirits.Presentation;
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
            .AddCard<ColorlessCardPool, SpiritsDoubleShot>()
            // Drinks as status cards: handed out by Tavern cards, auto-played on confirm or draw.
            .AddCard<StatusCardPool, SpiritsSip>()
            .AddCard<StatusCardPool, SpiritsDrink>()
            .AddCard<StatusCardPool, SpiritsShot>()
            .AddCard<StatusCardPool, SpiritsChug>()
            .AddCard<StatusCardPool, SpiritsFinishYourDrink>()
            // A curse that can turn up wherever random curses do.
            .AddCard<CurseCardPool, SpiritsSpill>()
            // One per character, in that character's normal rewards.
            .AddCard<IroncladCardPool, SpiritsKegStand>()
            .AddCard<SilentCardPool, SpiritsSlipAway>()
            .AddCard<DefectCardPool, SpiritsLiquidCooling>()
            .AddCard<RegentCardPool, SpiritsRoyalDecree>()
            .AddCard<NecrobinderCardPool, SpiritsPourOneOut>()
            .AddCard<FoolCardPool, SpiritsDrinkingSong>()
            .SetCardFilter(Rules.IsCardAllowed);
    }

    protected override void OnGameReady(ModContext context)
    {
        TabText.Apply(context.Loc, Rules.Effective);
        Rules.EffectiveChanged += rules => TabText.Apply(context.Loc, rules);
        Rules.LoadProfile();
    }
}
