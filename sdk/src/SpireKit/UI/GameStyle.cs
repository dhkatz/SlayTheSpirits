using Godot;

namespace SpireKit.UI;

/// <summary>The game's look for code-built UI: its palette, font and outlined text.</summary>
public static class GameStyle
{
    /// <summary>The game's gold (<c>StsColors.gold</c>): titles, focus, highlights.</summary>
    public static readonly Color Gold = new("EFC851");

    /// <summary>The game's cream (<c>StsColors.cream</c>): normal text.</summary>
    public static readonly Color Cream = new("FFF6E2");

    /// <summary>Dimmed cream (<c>StsColors.halfTransparentCream</c>): secondary text.</summary>
    public static readonly Color FadedCream = new("FFF6E280");

    /// <summary>The game's gray (<c>StsColors.gray</c>): disabled controls.</summary>
    public static readonly Color Gray = new(0.5f, 0.5f, 0.5f);

    /// <summary>Text outline, as on the game's labels.</summary>
    public static readonly Color Outline = new(0f, 0f, 0f, 0.9f);

    /// <summary>A label in the game's font with its outline and drop shadow.</summary>
    public static Label Label(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        ApplyText(label, size, color);
        return label;
    }

    /// <summary>Gives any text control (label, button, line edit) the game's font, outline and shadow.</summary>
    public static void ApplyText(Control control, int size, Color color)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (GameFonts.Default is { } font)
        {
            control.AddThemeFontOverride("font", font);
        }

        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeColorOverride("font_color", color);
        control.AddThemeColorOverride("font_outline_color", Outline);
        control.AddThemeConstantOverride("outline_size", Math.Max(4, size / 3));
        control.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
        control.AddThemeConstantOverride("shadow_offset_y", 3);
    }
}
