using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace SpireKit.Content;

/// <summary>
/// Base class for mod cards. Adds asset conventions on top of <see cref="CardModel"/>:
/// <list type="bullet">
/// <item>Custom art is read from <see cref="CustomPortraitPath"/> (a raw PNG in the mod's PCK) and served
/// through the <c>Portrait</c> texture.</item>
/// <item><see cref="PortraitPath"/> always points at an imported base-game portrait, so any game code that
/// loads the path directly still gets a valid resource (and cards without art still render).</item>
/// </list>
/// </summary>
public abstract class SpireCard(int energyCost, CardType type, CardRarity rarity, TargetType targetType, bool showInCardLibrary = true)
    : CardModel(energyCost, type, rarity, targetType, showInCardLibrary)
{
    /// <summary><c>res://&lt;ModId&gt;/images/cards/&lt;entry&gt;.png</c>.</summary>
    public string CustomPortraitPath => ModArt.PathFor("cards", Id);

    /// <summary>A base-game portrait used as the resource path and as art until custom art exists.</summary>
    protected virtual string PlaceholderPortraitPath => Type switch
    {
        CardType.Attack => ModelDb.Card<UltimateStrike>().PortraitPath,
        CardType.Power => ModelDb.Card<BeaconOfHope>().PortraitPath,
        _ => ModelDb.Card<Rally>().PortraitPath,
    };

    public override string PortraitPath => PlaceholderPortraitPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];
}
