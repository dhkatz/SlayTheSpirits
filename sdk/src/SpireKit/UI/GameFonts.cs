using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.addons.mega_text;

namespace SpireKit.UI;

/// <summary>
/// The game's fonts, borrowed from live UI. The Latin font paths only exist inside the game's scenes/themes,
/// but every <see cref="MegaLabel"/> is required to carry a font override, so any one of them yields the font.
/// </summary>
public static class GameFonts
{
    private static Font? _default;

    /// <summary>The game's standard label font, or null if no game UI exists yet.</summary>
    public static Font? Default => _default ??= Find();

    private static Font? Find()
    {
        if (NGame.Instance is not { } game)
        {
            return null;
        }

        var label = FindLabel(game);
        return label?.GetThemeFont(ThemeConstants.Label.Font);
    }

    private static MegaLabel? FindLabel(Node node)
    {
        if (node is MegaLabel label)
        {
            return label;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindLabel(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
