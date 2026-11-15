using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SpireKit.Content;
using SpireKit.UI;

namespace SlayTheSpirits.Tavern;

/// <summary>
/// The Tavern's screen, built in code: the bar, the ghostly bartender behind the counter, the party in front of it,
/// and the menu board. Clicking a menu row buys it through the game's shop sync.
/// </summary>
/// <remarks>Laid out for the game's 1920x1080 design resolution, like its own rooms.</remarks>
public sealed partial class NTavernRoom : Control, IScreenContext
{
    private static readonly Vector2 BartenderSize = new(500, 766);
    private static readonly Vector2 BartenderPos = new(620, 192);

    // As big as fits above the proceed button (the game parks it at about (1583, 764)).
    private static readonly Vector2 BoardPos = new(1140, 12);
    private static readonly Vector2 BoardSize = new(760, 744);

    // Where the slate is inside the board art's frame (left, top, right, bottom), measured on the art. The frame is
    // a nine-patch, so the rails keep their size however big the board is drawn.
    private static readonly (int Left, int Top, int Right, int Bottom) Slate = (84, 78, 82, 74);
    private const int SlatePadding = 14;
    private static readonly Vector2 PartyPos = new(520, 1010);

    private readonly List<Row> _rows = [];
    private IRunState _run = null!;
    private Player _me = null!;
    private TextureRect _bartender = null!;
    private Control _party = null!;
    private VBoxContainer _menu = null!;
    private Vector2 _menuSpace;
    private NProceedButton? _proceed;
    private bool _busy;

    public Control? DefaultFocusedControl => _rows.Count > 0 ? _rows[0].Panel : this;

    internal static NTavernRoom Create(IRunState run)
    {
        var room = new NTavernRoom { _run = run, _me = LocalContext.GetMe(run.Players) ?? run.Players[0] };
        room.Build();
        return room;
    }

    public override void _Ready()
    {
        NMapScreen.Instance?.SetTravelEnabled(enabled: true);
        if (ResourceLoader.Load<PackedScene>("res://scenes/ui/proceed_button.tscn") is { } scene)
        {
            _proceed = scene.Instantiate<NProceedButton>();
            AddChild(_proceed);
            _proceed.UpdateText(NProceedButton.ProceedLoc);
            _proceed.SetPulseState(isPulsing: false);
            _proceed.Initialize(_run);
            _proceed.Released += _ => RunManager.Instance.Proceed();
            _proceed.Enable();
        }

        AddParty();
        StartIdle();
        TavernAudio.StartMusic();
        TavernAudio.Voice(TavernAudio.Welcome);
        Say(TavernLines.Greetings, 3.0);
        Refresh();
        TaskHelper.RunSafely(FitMenuAsync());
    }

    public override void _ExitTree()
    {
        // Normally the music already stopped when the room was exited (and the act's music came back); this covers
        // leaving any other way, e.g. quitting to the menu from the bar.
        TavernAudio.StopMusic(restoreActMusic: false);
    }

    private void Build()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var background = FullScreen("tavern_background.png");
        background.SelfModulate = new Color(0.85f, 0.85f, 0.85f);
        AddChild(background);

        _bartender = new TextureRect
        {
            Texture = Art("tavern_bartender.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            Position = BartenderPos,
            Size = BartenderSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_bartender);
        AddChild(FullScreen("tavern_counter.png"));

        _party = new Control { Position = PartyPos, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_party);

        AddChild(BuildBoard());
    }

    private NinePatchRect BuildBoard()
    {
        var board = new NinePatchRect
        {
            Texture = Art("tavern_menu_board.png"),
            Position = BoardPos,
            Size = BoardSize,
            PatchMarginLeft = 170,
            PatchMarginRight = 170,
            PatchMarginTop = 165,
            PatchMarginBottom = 165,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        // The menu is laid out at full size, then scaled down if it's taller than the slate (FitMenuAsync).
        var left = Slate.Left + SlatePadding;
        var top = Slate.Top + SlatePadding;
        _menuSpace = new Vector2(BoardSize.X - left - Slate.Right - SlatePadding, BoardSize.Y - top - Slate.Bottom - SlatePadding);
        _menu = new VBoxContainer { Position = new Vector2(left, top), Size = new Vector2(_menuSpace.X, 0), MouseFilter = MouseFilterEnum.Ignore };
        _menu.AddThemeConstantOverride("separation", 2);
        board.AddChild(_menu);
        var list = _menu;
        list.AddChild(GameStyle.Label("The Spirited Tap", 34, GameStyle.Gold, HorizontalAlignment.Center));
        list.AddChild(GameStyle.Label("Paid in gold and drinks. One of each per visit.", 16, GameStyle.FadedCream, HorizontalAlignment.Center));

        var inventory = RunManager.Instance.MerchantSynchronizer.GetInventoryFor(_me);
        foreach (var entry in inventory.AllEntries.OfType<TavernEntry>())
        {
            var row = new Row(entry, this);
            _rows.Add(row);
            list.AddChild(row.Panel);
            entry.PurchaseCompleted += OnPurchaseCompleted;
            entry.PurchaseFailed += OnPurchaseFailed;
        }

        return board;
    }

    /// <summary>
    /// Shrinks the menu until it fits the slate. Wrapped descriptions make its height depend on its width, and a
    /// scaled-down menu gets more width per line, so this settles over a couple of layout passes.
    /// </summary>
    private async Task FitMenuAsync()
    {
        var scale = 1f;
        for (var pass = 0; pass < 4 && IsInstanceValid(this); pass++)
        {
            _menu.Scale = new Vector2(scale, scale);
            _menu.Size = new Vector2(_menuSpace.X / scale, 0);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInstanceValid(this))
            {
                return;
            }

            var needed = _menu.GetCombinedMinimumSize().Y;
            var next = needed > 0 ? Math.Min(1f, _menuSpace.Y / needed) : 1f;
            if (Math.Abs(next - scale) < 0.005f)
            {
                break;
            }

            scale = next;
        }
    }

    /// <summary>The party at the bar, using the game's shop poses: you in front, everyone else behind and dimmed.</summary>
    private void AddParty()
    {
        var players = _run.Players.OrderBy(p => p == _me ? 0 : 1).ToList();
        var x = 0f;
        for (var i = 0; i < players.Count; i++)
        {
            if (ResourceLoader.Load<PackedScene>(players[i].Character.MerchantAnimPath) is not { } scene)
            {
                continue;
            }

            var visual = scene.Instantiate<NMerchantCharacter>();
            _party.AddChild(visual);
            _party.MoveChild(visual, 0);
            visual.Position = new Vector2(x, i == 0 ? 0 : -50);
            if (i > 0)
            {
                visual.Modulate = new Color(0.5f, 0.5f, 0.5f);
            }

            x -= 275f;
        }
    }

    /// <summary>The bartender floats a little and flickers, as ghosts do.</summary>
    private void StartIdle()
    {
        var bob = CreateTween().SetLoops();
        bob.TweenProperty(_bartender, "position:y", BartenderPos.Y - 10, 2.2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bob.TweenProperty(_bartender, "position:y", BartenderPos.Y, 2.2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        var flicker = CreateTween().SetLoops();
        flicker.TweenProperty(_bartender, "modulate:a", 0.88f, 1.6).SetTrans(Tween.TransitionType.Sine);
        flicker.TweenProperty(_bartender, "modulate:a", 1f, 1.9).SetTrans(Tween.TransitionType.Sine);
    }

    private void Buy(TavernEntry entry)
    {
        if (_busy || !entry.IsStocked)
        {
            return;
        }

        if (!entry.EnoughGold)
        {
            TavernAudio.Voice(TavernAudio.Disappointed);
            Say(TavernLines.TooPoor);
            return;
        }

        TaskHelper.RunSafely(BuyAsync(entry));
    }

    private async Task BuyAsync(TavernEntry entry)
    {
        _busy = true;
        try
        {
            await RunManager.Instance.MerchantSynchronizer.PurchaseLocalEntry(entry);
        }
        finally
        {
            _busy = false;
            if (IsInstanceValid(this))
            {
                Refresh();
            }
        }
    }

    private void OnPurchaseCompleted(PurchaseStatus status, MerchantEntry entry)
    {
        if (IsInstanceValid(this) && entry is TavernEntry tavern)
        {
            TavernAudio.Voice(TavernAudio.Thanks);
            Say(TavernLines.For(tavern.Item));
            Refresh();
        }
    }

    private void OnPurchaseFailed(PurchaseStatus status)
    {
        if (IsInstanceValid(this) && status == PurchaseStatus.FailureGold)
        {
            TavernAudio.Voice(TavernAudio.Disappointed);
            Say(TavernLines.TooPoor);
        }
    }

    private void Refresh()
    {
        foreach (var row in _rows)
        {
            row.Refresh();
        }
    }

    private void Say(IReadOnlyList<string> lines, double seconds = 2.5)
    {
        var line = Rng.Chaotic.NextItem(lines);
        // The bubble grows to the left of this point (tail on the right), then drifts ~60px right as it pops in, so
        // anchor it at the bartender's far left, level with his head, to keep it off his face.
        var at = _bartender.GlobalPosition + new Vector2(BartenderSize.X * 0.06f, BartenderSize.Y * 0.1f);
        if (line is not null && NSpeechBubbleVfx.Create(line, DialogueSide.Right, at, seconds, VfxColor.Blue) is { } bubble)
        {
            this.AddChildSafely(bubble);
        }
    }

    private static Texture2D? Art(string file) => ModArt.Load(TavernRoom.ArtDir + file);

    private static TextureRect FullScreen(string file)
    {
        var rect = new TextureRect
        {
            Texture = Art(file),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        rect.SetAnchorsPreset(LayoutPreset.FullRect);
        return rect;
    }

    /// <summary>
    /// One line on the menu board: icon, name and effect, and the price in gold and drink. A container rather than a
    /// button so it grows with its text (descriptions wrap), which the menu's fit-to-slate scaling relies on.
    /// </summary>
    private sealed class Row
    {
        private static readonly Color Poor = new("E05A4A");
        private static readonly StyleBoxFlat Normal = Box(new Color(0, 0, 0, 0));
        private static readonly StyleBoxFlat Hover = Box(new Color(1, 1, 1, 0.08f));
        private readonly TavernEntry _entry;
        private readonly NTavernRoom _room;
        private readonly Label _gold;
        private readonly Label _drink;
        private bool _hovered;

        public Row(TavernEntry entry, NTavernRoom room)
        {
            _entry = entry;
            _room = room;
            Panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 76),
                FocusMode = FocusModeEnum.All,
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            Panel.AddThemeStyleboxOverride("panel", Normal);
            Panel.MouseEntered += () => SetHovered(true);
            Panel.MouseExited += () => SetHovered(false);
            Panel.FocusEntered += () => SetHovered(true);
            Panel.FocusExited += () => SetHovered(false);
            Panel.GuiInput += OnInput;

            var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            line.AddThemeConstantOverride("separation", 14);
            Panel.AddChild(line);

            line.AddChild(new TextureRect
            {
                Texture = Art($"{entry.Item.Id}.png") ?? Art("house_lager.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(64, 64),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            });

            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeConstantOverride("separation", 0);
            var name = GameStyle.Label(entry.Item.Name, 24, GameStyle.Cream);
            var description = GameStyle.Label(entry.Item.Description, 16, GameStyle.FadedCream);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.AddChild(name);
            text.AddChild(description);
            line.AddChild(text);

            var price = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(92, 0) };
            price.AddThemeConstantOverride("separation", 0);
            _gold = GameStyle.Label(string.Empty, 22, GameStyle.Gold, HorizontalAlignment.Right);
            _drink = GameStyle.Label(string.Empty, 16, GameStyle.Cream, HorizontalAlignment.Right);
            price.AddChild(_gold);
            price.AddChild(_drink);
            line.AddChild(price);

            foreach (var child in new Control[] { name, description, _gold, _drink })
            {
                child.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        public PanelContainer Panel { get; }

        public void Refresh()
        {
            var rules = SlayTheSpiritsMod.Instance?.Rules.Effective ?? Presets.Standard;
            _gold.Text = _entry.Bought ? "Sold" : _entry.Cost == 0 ? "Free" : $"{_entry.Cost}g";
            _gold.Modulate = !_entry.Bought && !_entry.EnoughGold ? Poor : Colors.White;
            _drink.Text = _entry.Item.Drink is { } kind ? "+ " + PenaltyLabels.Describe(rules, new Penalty(kind)) : string.Empty;
            Panel.Modulate = _entry.IsStocked ? Colors.White : new Color(1, 1, 1, 0.4f);
            Panel.MouseDefaultCursorShape = _entry.IsStocked ? CursorShape.PointingHand : CursorShape.Arrow;
            SetHovered(_hovered);
        }

        private void OnInput(InputEvent input)
        {
            var clicked = input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false };
            if ((clicked || input.IsActionPressed("ui_accept")) && _entry.IsStocked)
            {
                Panel.AcceptEvent();
                _room.Buy(_entry);
            }
        }

        private void SetHovered(bool hovered)
        {
            _hovered = hovered;
            Panel.AddThemeStyleboxOverride("panel", hovered && _entry.IsStocked ? Hover : Normal);
        }

        private static StyleBoxFlat Box(Color color) => new()
        {
            BgColor = color,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 8,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
    }
}
