using Godot;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.UI.Controls;

/// <summary>
/// A developer panel showing every SpireKit control with sample settings, for checking looks and input in the
/// real game. Only exists when the game is started with <see cref="Flag"/> (<c>tools/launch.ps1 -Gallery</c>);
/// F8 toggles it.
/// </summary>
public sealed partial class ControlsGallery : CanvasLayer
{
    /// <summary>Command-line flag that opens the gallery at startup.</summary>
    public const string Flag = "--spirekit-gallery";

    private readonly List<Control> _focusables = [];

    public ControlsGallery()
    {
        Layer = 110;
        Name = "SpireKitControlsGallery";

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = Control.MouseFilterEnum.Ignore };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        root.AddChild(backdrop);
        root.AddChild(center);
        center.AddChild(BuildPanel());
        AddChild(root, @internal: InternalMode.Front);
    }

    /// <summary>Adds the gallery to the game if it was started with <see cref="Flag"/>.</summary>
    internal static void ShowIfRequested(ModContext context)
    {
        if (!OS.GetCmdlineArgs().Contains(Flag) || Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        context.Log.Info("Controls gallery open (F8 toggles).");
        tree.Root.CallDeferred(Node.MethodName.AddChild, new ControlsGallery());
    }

    public override void _Ready() => FocusFirst();

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 })
        {
            Visible = !Visible;
            if (Visible)
            {
                FocusFirst();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void FocusFirst()
    {
        if (_focusables.Count > 0)
        {
            _focusables[0].CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private PanelContainer BuildPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.09f, 0.95f),
            BorderColor = GameStyle.Gold,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 40,
            ContentMarginRight = 40,
            ContentMarginTop = 24,
            ContentMarginBottom = 24,
        });

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 14);
        rows.AddChild(GameStyle.Label("SpireKit controls", 36, GameStyle.Gold, HorizontalAlignment.Center));
        rows.AddChild(GameStyle.Label("Click, or focus and use left/right. Tab moves focus. F8 hides this.", 18, GameStyle.FadedCream, HorizontalAlignment.Center));

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 32);
        grid.AddThemeConstantOverride("v_separation", 12);
        AddStepper(grid, "Number (0-10)", new NumberStepper { Max = 10, Value = 3 });
        AddStepper(grid, "Formatted", new NumberStepper
        {
            Max = 5,
            Value = 1,
            Format = static v => v switch { 0 => "Off", 1 => "1 Shot", _ => $"{v} Shots" },
        });
        AddStepper(grid, "Steps of 5", new NumberStepper { Max = 50, Step = 5, Value = 15, Format = static v => $"≥ {v}" });
        AddStepper(grid, "Read-only", new NumberStepper { Max = 10, Value = 2, Editable = false });
        rows.AddChild(grid);

        var close = new Button { Text = "Close (F8)", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        GameStyle.ApplyText(close, 22, GameStyle.Cream);
        close.AddThemeColorOverride("font_hover_color", GameStyle.Gold);
        close.Pressed += () => Visible = false;
        rows.AddChild(close);

        panel.AddChild(rows);
        return panel;
    }

    private void AddStepper(GridContainer grid, string name, NumberStepper stepper)
    {
        var readout = GameStyle.Label(string.Empty, 18, GameStyle.FadedCream);
        readout.CustomMinimumSize = new Vector2(220, 0);
        stepper.ValueChanged += value => readout.Text = $"ValueChanged → {value}";

        grid.AddChild(GameStyle.Label(name, 24, GameStyle.Cream));
        grid.AddChild(stepper);
        grid.AddChild(readout);
        if (stepper.Editable)
        {
            _focusables.Add(stepper);
        }
    }
}
