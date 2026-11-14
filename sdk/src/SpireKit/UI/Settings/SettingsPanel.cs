using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SpireKit.Logging;

namespace SpireKit.UI.Settings;

/// <summary>
/// The panel behind a mod's Settings tab. It is the game's <see cref="NSettingsPanel"/> (fade-in, scrolling, sizing)
/// with content rebuilt by the mod each time the tab is shown, and controller navigation that includes mod controls.
/// </summary>
public partial class SettingsPanel : NSettingsPanel
{
    private const float MinPadding = 50f;

    private Action<SettingsPage>? _build;
    private SettingsTemplates? _templates;
    private bool _dirty = true;

    public SettingsPanel()
    {
        AddChild(new VBoxContainer { Name = "VBoxContainer" });
    }

    internal void Configure(Action<SettingsPage> build, SettingsTemplates templates)
    {
        _build = build;
        _templates = templates;
        templates.CopyPanelLayout(this, GetNode<VBoxContainer>("VBoxContainer"));
    }

    /// <summary>Rebuilds the page now if it's showing, or the next time it's shown.</summary>
    public void Invalidate()
    {
        _dirty = true;
        if (IsVisibleInTree())
        {
            Rebuild();
        }
    }

    protected override void OnVisibilityChange()
    {
        if (!Visible)
        {
            _dirty = true; // Always show current state when the tab is opened again.
        }
        else if (_dirty)
        {
            Rebuild();
        }

        base.OnVisibilityChange();
    }

    /// <summary>One vertical chain through every focusable control (left/right are left to the controls).</summary>
    protected override void UpdateNavigation()
    {
        var chain = new List<Control>();
        Collect(Content, chain);
        for (var i = 0; i < chain.Count; i++)
        {
            var self = chain[i].GetPath();
            chain[i].FocusNeighborLeft = self;
            chain[i].FocusNeighborRight = self;
            chain[i].FocusNeighborTop = i > 0 ? chain[i - 1].GetPath() : self;
            chain[i].FocusNeighborBottom = i < chain.Count - 1 ? chain[i + 1].GetPath() : self;
            chain[i].FocusPrevious = chain[i].FocusNeighborTop;
            chain[i].FocusNext = chain[i].FocusNeighborBottom;
        }
    }

    private void Rebuild()
    {
        if (_build is null || _templates is null || Content is null)
        {
            return;
        }

        _dirty = false;
        var before = new List<Control>();
        Collect(Content, before);
        var focused = before.FindIndex(static c => c.HasFocus());
        foreach (var child in Content.GetChildren())
        {
            Content.RemoveChild(child);
            child.QueueFree();
        }

        try
        {
            _build(new SettingsPage(Content, _templates, Invalidate));
        }
#pragma warning disable CA1031 // A broken page must not break the game's Settings screen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Content.AddChild(GameStyle.Label($"This page failed to load: {ex.Message}", 22, GameStyle.Cream));
            Modding.SpireRuntime.Current?.Log.Error("Settings page failed to build.", ex);
        }

        UpdateNavigation();
        Callable.From(Resize).CallDeferred();
        if (focused >= 0)
        {
            var after = new List<Control>();
            Collect(Content, after);
            if (after.Count > 0)
            {
                after[Math.Min(focused, after.Count - 1)].CallDeferred(Control.MethodName.GrabFocus);
            }
        }
    }

    /// <summary>Same sizing rule as the game's panels: room to scroll the last rows up when the content overflows.</summary>
    private void Resize()
    {
        if (GetParent() is not Control parent || Content is null)
        {
            return;
        }

        var minimum = Content.GetCombinedMinimumSize();
        Size = minimum.Y + MinPadding >= parent.Size.Y
            ? new Vector2(Content.Size.X, minimum.Y + (parent.Size.Y * 0.4f))
            : new Vector2(Content.Size.X, minimum.Y);
    }

    private static void Collect(Node node, List<Control> chain)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Control { Visible: true } control)
            {
                continue;
            }

            if (control.FocusMode == FocusModeEnum.All)
            {
                chain.Add(control);
            }
            else
            {
                Collect(control, chain);
            }
        }
    }
}
