using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace SpireKit.UI.Settings;

/// <summary>The game's settings tickbox (art, sounds, focus reticle) driving a mod setting.</summary>
public partial class SettingsToggle : NSettingsTickbox
{
    private bool _initial;

    public SettingsToggle()
    {
        GameScenes.Graft(this, GameScenes.SettingsTickbox);
        CustomMinimumSize = new Godot.Vector2(Math.Max(CustomMinimumSize.X, 320), Math.Max(CustomMinimumSize.Y, 64));
    }

    /// <summary>Raised when the player ticks or unticks it (not when <see cref="SetTicked"/> is called).</summary>
    public event Action<bool>? Changed;

    /// <summary>Sets the state without raising <see cref="Changed"/>; safe before the control is in the tree.</summary>
    public void SetTicked(bool ticked)
    {
        _initial = ticked;
        if (IsNodeReady())
        {
            IsTicked = ticked;
        }
    }

    /// <summary>Greys it out and ignores clicks (read-only rules).</summary>
    public void SetEditable(bool editable)
    {
        if (editable)
        {
            Enable();
        }
        else
        {
            Disable();
        }

        FocusMode = editable ? FocusModeEnum.All : FocusModeEnum.None;
    }

    public override void _Ready()
    {
        ConnectSignals();
        IsTicked = _initial;
    }

    protected override void OnTick() => Changed?.Invoke(true);

    protected override void OnUntick() => Changed?.Invoke(false);
}
