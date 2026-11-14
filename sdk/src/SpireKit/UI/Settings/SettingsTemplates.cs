using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace SpireKit.UI.Settings;

/// <summary>
/// Pieces copied from the game's own General settings panel so mod rows look identical: the 64px line container,
/// its label, and the thin divider. Each falls back to a <see cref="GameStyle"/> look if the game's layout changes.
/// </summary>
internal sealed class SettingsTemplates
{
    private const string TemplateLine = "FastMode";
    private const string TemplateDivider = "CombatSpeedDivider";

    private readonly NSettingsPanel? _general;
    private readonly Control? _line;
    private readonly RichTextLabel? _label;
    private readonly ColorRect? _divider;

    public SettingsTemplates(NSettingsPanel? general)
    {
        _general = general;
        var content = general?.GetNodeOrNull<VBoxContainer>("VBoxContainer");
        _line = content?.GetNodeOrNull<Control>(TemplateLine);
        _label = _line?.GetNodeOrNull<RichTextLabel>("Label");
        _divider = content?.GetNodeOrNull<ColorRect>(TemplateDivider);
    }

    /// <summary>Whether the game's pieces were all found (otherwise rows use fallbacks).</summary>
    public bool IsComplete => _general is not null && _line is not null && _label is not null && _divider is not null;

    public void CopyPanelLayout(Control panel, VBoxContainer content)
    {
        if (_general is null)
        {
            return;
        }

        GameScenes.CopyProperties(_general, panel, "visible");
        if (_general.GetNodeOrNull<VBoxContainer>("VBoxContainer") is { } generalContent)
        {
            GameScenes.CopyProperties(generalContent, content);
        }
    }

    /// <summary>A settings line: label on the left, controls on the right, 64px tall.</summary>
    public MarginContainer NewLine()
    {
        var line = new MarginContainer();
        if (_line is not null)
        {
            GameScenes.CopyProperties(_line, line);
        }
        else
        {
            line.CustomMinimumSize = new Vector2(0, 64);
        }

        return line;
    }

    public Control NewLabel(string text)
    {
        if (_label?.Duplicate() is RichTextLabel label)
        {
            label.Text = text;
            return label;
        }

        var fallback = GameStyle.Label(text, 28, GameStyle.Cream);
        fallback.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return fallback;
    }

    public Control NewDivider()
    {
        if (_divider?.Duplicate() is ColorRect divider)
        {
            divider.Visible = true;
            return divider;
        }

        return new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = new Color(1f, 1f, 1f, 0.1f) };
    }
}
