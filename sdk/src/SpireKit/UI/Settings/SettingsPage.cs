using Godot;
using SpireKit.UI.Controls;

namespace SpireKit.UI.Settings;

/// <summary>
/// Builds the rows of a mod's Settings tab, in the game's style: each row is a label on the left and controls on
/// the right, separated by the game's dividers. Handed to the tab's build callback each time the tab is shown.
/// </summary>
/// <remarks>
/// Build from current state every time and write changes straight back (the page is rebuilt, never diffed).
/// Call <see cref="Refresh"/> when a change alters which rows exist.
/// </remarks>
public sealed class SettingsPage
{
    private readonly VBoxContainer _content;
    private readonly SettingsTemplates _templates;
    private readonly Action _invalidate;
    private bool _afterHeader = true;

    internal SettingsPage(VBoxContainer content, SettingsTemplates templates, Action invalidate)
    {
        _content = content;
        _templates = templates;
        _invalidate = invalidate;
    }

    /// <summary>Rebuilds the whole page (next frame), keeping focus on the same position.</summary>
    public void Refresh() => Callable.From(_invalidate).CallDeferred();

    /// <summary>A gold section title.</summary>
    public void Header(string text)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", _content.GetChildCount() == 0 ? 4 : 28);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        margin.AddChild(GameStyle.Label(text, 32, GameStyle.Gold));
        _content.AddChild(margin);
        _afterHeader = true;
    }

    /// <summary>A line of smaller, dimmed explanatory text.</summary>
    public void Note(string text)
    {
        var label = GameStyle.Label(text, 20, GameStyle.FadedCream);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(0, 30);
        _content.AddChild(label);
    }

    /// <summary>An on/off setting using the game's tickbox.</summary>
    public SettingsToggle Toggle(string label, bool value, Action<bool>? changed, bool editable = true)
    {
        var toggle = new SettingsToggle();
        toggle.SetTicked(value);
        if (changed is not null)
        {
            toggle.Changed += changed;
        }

        AddLine(label, toggle);
        toggle.SetEditable(editable);
        return toggle;
    }

    /// <summary>A pick-one setting using the game's option picker.</summary>
    public SettingsChoice Choice(string label, IReadOnlyList<string> options, int index, Action<int>? changed, bool editable = true)
    {
        var choice = new SettingsChoice();
        choice.SetOptions(options, index);
        if (changed is not null)
        {
            choice.Changed += changed;
        }

        AddLine(label, choice);
        choice.SetEditable(editable);
        return choice;
    }

    /// <summary>A whole-number setting.</summary>
    public NumberStepper Number(string label, int value, int min, int max, Action<int>? changed, Func<int, string>? format = null, bool editable = true)
    {
        var stepper = new NumberStepper { Min = min, Max = max, Format = format, LabelWidth = 160 };
        stepper.SetValueNoSignal(value);
        if (changed is not null)
        {
            stepper.ValueChanged += v => changed(v);
        }

        stepper.Editable = editable;
        AddLine(label, stepper);
        return stepper;
    }

    /// <summary>A row with any controls on the right, e.g. several compact steppers.</summary>
    public HBoxContainer Row(string label, params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        var box = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        box.AddThemeConstantOverride("separation", 12);
        foreach (var control in controls)
        {
            control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            box.AddChild(control);
        }

        AddLine(label, box);
        return box;
    }

    /// <summary>A row of buttons, centered.</summary>
    public HBoxContainer Buttons(params (string Text, Action Pressed)[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        AddDividerIfNeeded();
        var box = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 72) };
        box.AddThemeConstantOverride("separation", 24);
        foreach (var (text, pressed) in buttons)
        {
            var button = GameButton(text);
            button.Pressed += pressed;
            box.AddChild(button);
        }

        _content.AddChild(box);
        return box;
    }

    /// <summary>A button styled like the game's: dark plate, gold edge on hover/focus.</summary>
    public static Button GameButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(240, 56),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        GameStyle.ApplyText(button, 22, GameStyle.Cream);
        button.AddThemeColorOverride("font_hover_color", GameStyle.Gold);
        button.AddThemeColorOverride("font_focus_color", GameStyle.Gold);
        button.AddThemeColorOverride("font_pressed_color", GameStyle.Gold);
        button.AddThemeStyleboxOverride("normal", Plate(new Color(0.08f, 0.1f, 0.13f, 0.9f), new Color(1f, 1f, 1f, 0.15f)));
        button.AddThemeStyleboxOverride("hover", Plate(new Color(0.12f, 0.14f, 0.18f, 0.95f), GameStyle.Gold));
        button.AddThemeStyleboxOverride("focus", Plate(new Color(0f, 0f, 0f, 0f), GameStyle.Gold));
        button.AddThemeStyleboxOverride("pressed", Plate(new Color(0.05f, 0.06f, 0.08f, 0.95f), GameStyle.Gold));
        return button;
    }

    private static StyleBoxFlat Plate(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 20,
        ContentMarginRight = 20,
    };

    private void AddLine(string label, Control control)
    {
        AddDividerIfNeeded();
        var line = _templates.NewLine();
        var text = _templates.NewLabel(label);
        control.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        line.AddChild(text);
        line.AddChild(control);
        _content.AddChild(line);
    }

    private void AddDividerIfNeeded()
    {
        if (!_afterHeader)
        {
            _content.AddChild(_templates.NewDivider());
        }

        _afterHeader = false;
    }
}
