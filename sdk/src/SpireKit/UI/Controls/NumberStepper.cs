using Godot;
using MegaCrit.Sts2.Core.ControllerInput;

namespace SpireKit.UI.Controls;

/// <summary>
/// A whole-number picker drawn as <c>‹ 3 ›</c> with the game's settings arrows. Click the arrows, or focus it and
/// press left/right (keyboard or controller; held keys repeat).
/// </summary>
/// <remarks>
/// Create it with <c>new</c> and set <see cref="Min"/>/<see cref="Max"/> before <see cref="Value"/>.
/// Its parts are internal children, so <see cref="Node.GetChildren"/> and duplication ignore them.
/// </remarks>
public partial class NumberStepper : HBoxContainer
{
    private const int FontSize = 26;
    private const int CompactFontSize = 22;
    private const string ArrowTexture = "res://images/packed/common_ui/settings_tiny_left_arrow.png";

    private readonly BaseButton _down;
    private readonly BaseButton _up;
    private readonly Label _label;
    private int _min;
    private int _max = 99;
    private static Texture2D? _arrow;
    private static bool _arrowLoaded;
    private int _value;
    private bool _editable = true;
    private Func<int, string>? _format;
    private bool _compact;

    public NumberStepper()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Alignment = AlignmentMode.Center;
        AddThemeConstantOverride("separation", 4);

        _down = MakeArrow("‹", -1);
        _label = GameStyle.Label(string.Empty, FontSize, GameStyle.Cream, HorizontalAlignment.Center);
        _label.CustomMinimumSize = new Vector2(72, 0);
        _up = MakeArrow("›", 1);
        AddChild(_down, @internal: InternalMode.Front);
        AddChild(_label, @internal: InternalMode.Front);
        AddChild(_up, @internal: InternalMode.Front);

        FocusEntered += Refresh;
        FocusExited += Refresh;
        Refresh();
    }

    /// <summary>Raised when the value changes, by the player or by setting <see cref="Value"/>.</summary>
    [Signal]
    public delegate void ValueChangedEventHandler(int value);

    /// <summary>Lowest allowed value (inclusive).</summary>
    public int Min
    {
        get => _min;
        set
        {
            _min = value;
            _max = Math.Max(_max, value);
            Value = _value;
        }
    }

    /// <summary>Highest allowed value (inclusive).</summary>
    public int Max
    {
        get => _max;
        set
        {
            _max = value;
            _min = Math.Min(_min, value);
            Value = _value;
        }
    }

    /// <summary>How much one press changes the value.</summary>
    public int Step { get; set; } = 1;

    /// <summary>
    /// Shrinks the arrows and text so several steppers fit on one settings row (e.g. a trigger's target, amount
    /// and drink).
    /// </summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            var size = value ? CompactFontSize : FontSize;
            GameStyle.ApplyText(_label, size, GameStyle.Cream);
            foreach (var arrow in new[] { _down, _up })
            {
                arrow.CustomMinimumSize = value ? new Vector2(34, 34) : new Vector2(44, 44);
            }

            Refresh();
        }
    }

    /// <summary>Whether stepping past one end continues from the other (for choices rather than amounts).</summary>
    public bool Wrap { get; set; }

    /// <summary>Width reserved for the value text, so the arrows don't move as it changes.</summary>
    public float LabelWidth
    {
        get => _label.CustomMinimumSize.X;
        set => _label.CustomMinimumSize = new Vector2(value, 0);
    }

    /// <summary>The current value, clamped to <see cref="Min"/>..<see cref="Max"/>.</summary>
    public int Value
    {
        get => _value;
        set
        {
            if (SetValueNoSignal(value))
            {
                EmitSignal(SignalName.ValueChanged, _value);
            }
        }
    }

    /// <summary>Whether the player can change the value (false shows it grayed out, e.g. read-only rules).</summary>
    public bool Editable
    {
        get => _editable;
        set
        {
            _editable = value;
            Refresh();
        }
    }

    /// <summary>How the value is displayed, e.g. <c>v => v == 0 ? "Off" : $"{v}"</c>. Defaults to the number.</summary>
    public Func<int, string>? Format
    {
        get => _format;
        set
        {
            _format = value;
            Refresh();
        }
    }

    /// <summary>Sets the value without raising <see cref="ValueChanged"/>. Returns whether it changed.</summary>
    public bool SetValueNoSignal(int value)
    {
        var clamped = Math.Clamp(value, _min, _max);
        if (clamped == _value)
        {
            Refresh();
            return false;
        }

        _value = clamped;
        Refresh();
        return true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_editable || @event is null)
        {
            return;
        }

        if (@event.IsActionPressed(MegaInput.left, allowEcho: true))
        {
            Nudge(-1);
            AcceptEvent();
        }
        else if (@event.IsActionPressed(MegaInput.right, allowEcho: true))
        {
            Nudge(1);
            AcceptEvent();
        }
    }

    private void Nudge(int direction)
    {
        if (!_editable)
        {
            return;
        }

        var next = _value + (direction * Step);
        if (Wrap && _max > _min)
        {
            next = next > _max ? _min : next < _min ? _max : next;
        }

        Value = next;
    }

    private BaseButton MakeArrow(string text, int direction)
    {
        BaseButton button;
        if (Arrow() is { } texture)
        {
            var arrow = new TextureButton
            {
                TextureNormal = texture,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                FlipH = direction > 0,
                CustomMinimumSize = new Vector2(44, 44),
            };
            arrow.MouseEntered += () => arrow.Modulate = arrow.Disabled ? arrow.Modulate : new Color(1.3f, 1.3f, 1.3f);
            arrow.MouseExited += () => arrow.Modulate = arrow.Disabled ? DisabledArrow : Colors.White;
            button = arrow;
        }
        else
        {
            var fallback = new Button { Text = text, Flat = true };
            GameStyle.ApplyText(fallback, FontSize + 6, GameStyle.Cream);
            fallback.AddThemeColorOverride("font_hover_color", GameStyle.Gold);
            fallback.AddThemeColorOverride("font_disabled_color", GameStyle.Gray);
            button = fallback;
        }

        button.FocusMode = FocusModeEnum.None;
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
        button.Pressed += () =>
        {
            Nudge(direction);
            GrabFocus();
        };
        return button;
    }

    private static readonly Color DisabledArrow = new(0.35f, 0.35f, 0.35f, 0.6f);

    /// <summary>The game's settings arrow, or null outside the game (falls back to text arrows).</summary>
    private static Texture2D? Arrow()
    {
        if (!_arrowLoaded)
        {
            _arrowLoaded = true;
            _arrow = ResourceLoader.Exists(ArrowTexture) ? ResourceLoader.Load<Texture2D>(ArrowTexture) : null;
        }

        return _arrow;
    }

    private void Refresh()
    {
        _label.Text = _format?.Invoke(_value) ?? _value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        var color = !_editable ? GameStyle.Gray : HasFocus() ? GameStyle.Gold : GameStyle.Cream;
        _label.AddThemeColorOverride("font_color", color);
        _down.Disabled = !_editable || (!Wrap && _value <= _min);
        _up.Disabled = !_editable || (!Wrap && _value >= _max);
        foreach (var arrow in new[] { _down, _up })
        {
            if (arrow is TextureButton)
            {
                arrow.Modulate = arrow.Disabled ? DisabledArrow : Colors.White;
            }
        }
    }
}
