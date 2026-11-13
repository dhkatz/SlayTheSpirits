using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Cards;

/// <summary>
/// A drink as a status card: "Shot — Take a Shot. Gain 3 Tipsy." It is never played by hand; it auto-plays
/// when handed over (after the drinker confirms) or when drawn, and exhausts.
/// </summary>
/// <remarks>
/// Lives in the game's status pool, so it never appears in rewards or shops and vanishes after combat.
/// The penalty still goes through the engine when it plays, so caps, immunity and redirects apply then.
/// </remarks>
public abstract class SpiritsDrinkCard(PenaltyKind kind) : SpiritsCard(-1, CardType.Status, CardRarity.Status, TargetType.None)
{
    internal const string DrinkVar = "Drink";
    internal const string TipsyVar = "Tipsy";

    public PenaltyKind Kind { get; } = kind;

    /// <summary>Why this drink exists (the card that handed it out). Set when created.</summary>
    internal PenaltyCause? Cause { get; set; }

    /// <summary>Who handed it out, if not the drinker.</summary>
    internal PlayerId? GivenBy { get; set; }

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar(DrinkVar, PenaltyLabels.Defaults[Kind].Singular),
        new DynamicVar(TipsyVar, TipsyWeights.Defaults[Kind]),
    ];

    protected override IEnumerable<PenaltyKind> PenaltyKeywords => [Kind];

    public override bool CanBeGeneratedInCombat => false;

    /// <summary>Only ever auto-played (auto-play ignores this; manual play respects it).</summary>
    protected override bool IsPlayable => false;

    /// <summary>The host's label for this drink, e.g. "Shot of pickle juice" under the Sober preset.</summary>
    public override string Title => PenaltyLabels.For(DrinkCards.Rules, Kind).Singular;

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
        {
            await CardCmd.AutoPlay(choiceContext, this, null);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (SlayTheSpiritsMod.Game is { } game)
        {
            await game.PenalizeAsync(
                new PenaltyRequest(Penalty.Of(Kind), TargetKind.Subject, new PlayerId(Owner.NetId), GivenBy, Cause ?? new PenaltyCause(Id.ToString(), Title)),
                choiceContext);
        }
    }
}

public sealed class SpiritsSip() : SpiritsDrinkCard(PenaltyKind.Sip);

public sealed class SpiritsDrink() : SpiritsDrinkCard(PenaltyKind.Drink);

public sealed class SpiritsShot() : SpiritsDrinkCard(PenaltyKind.Shot);

public sealed class SpiritsChug() : SpiritsDrinkCard(PenaltyKind.Chug);

public sealed class SpiritsFinishYourDrink() : SpiritsDrinkCard(PenaltyKind.Finish);

/// <summary>Creates drink status cards and hands them out.</summary>
internal static class DrinkCards
{
    /// <summary>The rules in effect (falls back to Standard outside a session, e.g. the card library).</summary>
    public static RuleSet Rules => SlayTheSpiritsMod.Instance?.Rules.Effective ?? Presets.Standard;

    public static IReadOnlyList<Type> All { get; } =
        [typeof(SpiritsSip), typeof(SpiritsDrink), typeof(SpiritsShot), typeof(SpiritsChug), typeof(SpiritsFinishYourDrink)];

    /// <summary>Creates a drink card owned by <paramref name="drinker"/>, its text reflecting the current rules.</summary>
    public static SpiritsDrinkCard Create(ICombatState combat, Player drinker, PenaltyKind kind, PenaltyCause cause, Player? giver)
    {
        var type = kind switch
        {
            PenaltyKind.Sip => typeof(SpiritsSip),
            PenaltyKind.Drink => typeof(SpiritsDrink),
            PenaltyKind.Shot => typeof(SpiritsShot),
            PenaltyKind.Chug => typeof(SpiritsChug),
            PenaltyKind.Finish => typeof(SpiritsFinishYourDrink),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No drink card for this kind."),
        };

        var card = (SpiritsDrinkCard)combat.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), drinker);
        card.Cause = cause;
        card.GivenBy = giver is null || giver == drinker ? null : new PlayerId(giver.NetId);
        ((StringVar)card.DynamicVars[SpiritsDrinkCard.DrinkVar]).StringValue = PenaltyLabels.For(Rules, kind).Singular;
        card.DynamicVars[SpiritsDrinkCard.TipsyVar].BaseValue = TipsyWeights.PerUnit(Rules, kind);
        return card;
    }

    /// <summary>
    /// Has <paramref name="drinker"/> confirm a drink on the card-selection screen (when the rules ask for it
    /// and someone else is handing it out). Runs on the drinker's machine; others wait for their answer.
    /// </summary>
    public static async Task<bool> ConfirmAsync(PlayerChoiceContext choiceContext, SpiritsDrinkCard card, Player drinker)
    {
        if (!Rules.ConfirmDrinks || card.GivenBy is null)
        {
            return true;
        }

        return await CardSelectCmd.FromChooseACardScreen(choiceContext, [card], drinker, canSkip: false) is not null;
    }

    public static Task PlayAsync(PlayerChoiceContext choiceContext, SpiritsDrinkCard card) =>
        CardCmd.AutoPlay(choiceContext, card, null);
}
