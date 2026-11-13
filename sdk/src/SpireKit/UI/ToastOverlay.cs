using Godot;
using SpireKit.Logging;

namespace SpireKit.UI;

/// <summary>What to show in a toast.</summary>
/// <param name="Title">Headline, e.g. "Dave: SHOT".</param>
/// <param name="Body">Optional detail line, e.g. "Boss fight: The Guardian".</param>
/// <param name="Accent">Border/title color; defaults to the game's gold.</param>
/// <param name="Seconds">How long it stays fully visible.</param>
public sealed record Toast(string Title, string? Body = null, Color? Accent = null, double Seconds = 4);

/// <summary>
/// Stacked, non-interactive notifications drawn above everything (including menus and combat).
/// </summary>
/// <remarks>
/// Built from stock Godot controls rather than custom <see cref="Node"/> subclasses: the game only registers
/// Godot scripts from its own assembly, so mod-defined node types are best avoided.
/// Must be called on the main thread (all game hooks are).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Godot nodes are owned and freed by the scene tree.")]
public sealed class ToastOverlay(IModLog log)
{
    /// <summary>The game's gold (<c>StsColors.gold</c>).</summary>
    public static readonly Color Gold = new("EFC851");

    /// <summary>The game's cream text color.</summary>
    public static readonly Color Cream = new("FFF6E2");

    private const int MaxVisible = 6;
    private const int Layer = 120;

    private CanvasLayer? _layer;
    private VBoxContainer? _stack;

    public void Show(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        if (!EnsureLayer() || _stack is null)
        {
            log.Debug($"Toast dropped (no scene tree): {toast.Title}");
            return;
        }

        while (_stack.GetChildCount() >= MaxVisible)
        {
            var oldest = _stack.GetChild(0);
            _stack.RemoveChild(oldest);
            oldest.QueueFree();
        }

        var panel = Build(toast);
        _stack.AddChild(panel);

        var tween = panel.CreateTween();
        tween.TweenProperty(panel, "modulate:a", 1f, 0.15);
        tween.TweenInterval(toast.Seconds);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.5);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }

    private static PanelContainer Build(Toast toast)
    {
        // Styled after the game's reward/turn banners: a dark band with thin accent rules, game font, outlined text.
        var accent = toast.Accent ?? Gold;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.78f),
            BorderColor = accent,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 48,
            ContentMarginRight = 48,
            ContentMarginTop = 8,
            ContentMarginBottom = 10,
            ShadowColor = new Color(0f, 0f, 0f, 0.5f),
            ShadowSize = 12,
        };

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        panel.AddThemeStyleboxOverride("panel", style);

        var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        lines.AddChild(MakeLabel(toast.Title, 34, accent));
        if (!string.IsNullOrWhiteSpace(toast.Body))
        {
            lines.AddChild(MakeLabel(toast.Body, 22, Cream));
        }

        panel.AddChild(lines);
        return panel;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (GameFonts.Default is { } font)
        {
            label.AddThemeFontOverride("font", font);
        }

        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("outline_size", 10);
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_y", 3);
        return label;
    }

    private bool EnsureLayer()
    {
        if (_layer is not null && GodotObject.IsInstanceValid(_layer))
        {
            return true;
        }

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return false;
        }

        _layer = new CanvasLayer { Layer = Layer, Name = "SpireKitToasts" };
        _stack = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        _stack.AddThemeConstantOverride("separation", 8);
        _stack.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _stack.GrowHorizontal = Control.GrowDirection.Both;
        _stack.Position = new Vector2(0, 90);
        _layer.AddChild(_stack);
        tree.Root.CallDeferred(Node.MethodName.AddChild, _layer);
        return true;
    }
}
