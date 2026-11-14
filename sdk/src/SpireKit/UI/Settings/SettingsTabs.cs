using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SpireKit.Logging;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.UI.Settings;

/// <summary>A mod tab on the game's Settings screen.</summary>
/// <param name="Title">Tab label, e.g. "Spirits". Keep it short: tabs are 256px wide.</param>
/// <param name="Build">Fills the page; called each time the tab is shown (and on <see cref="SettingsPage.Refresh"/>).</param>
public sealed record SettingsTab(string Title, Action<SettingsPage> Build);

/// <summary>
/// Adds tabs to the game's Settings screen, next to General/Graphics/Sound/Input. The screen is the same from the
/// main menu and the pause menu, so a tab is reachable mid-run too (show read-only state there).
/// </summary>
public sealed class SettingsTabRegistry
{
    private readonly List<SettingsTabHandle> _tabs = [];

    /// <summary>Registers a tab. Call from <c>OnInitialize</c> or <c>OnGameReady</c>.</summary>
    public SettingsTabHandle Add(SettingsTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var handle = new SettingsTabHandle(tab);
        _tabs.Add(handle);
        return handle;
    }

    /// <summary>Registers a tab.</summary>
    public SettingsTabHandle Add(string title, Action<SettingsPage> build) => Add(new SettingsTab(title, build));

    internal IReadOnlyList<SettingsTabHandle> Tabs => _tabs;
}

/// <summary>A registered tab. Use <see cref="Refresh"/> when the state it shows changes from elsewhere.</summary>
public sealed class SettingsTabHandle
{
    private readonly List<WeakReference<SettingsPanel>> _panels = [];

    internal SettingsTabHandle(SettingsTab tab) => Tab = tab;

    public SettingsTab Tab { get; }

    /// <summary>Rebuilds the tab's page on every open Settings screen (or marks it to rebuild when next shown).</summary>
    public void Refresh()
    {
        _panels.RemoveAll(static weak => !weak.TryGetTarget(out var panel) || !GodotObject.IsInstanceValid(panel));
        foreach (var weak in _panels)
        {
            if (weak.TryGetTarget(out var panel))
            {
                panel.Invalidate();
            }
        }
    }

    internal void Track(SettingsPanel panel) => _panels.Add(new WeakReference<SettingsPanel>(panel));
}

/// <summary>Injects registered tabs when a Settings screen sets up its tabs.</summary>
[HarmonyPatch(typeof(NSettingsTabManager), nameof(NSettingsTabManager._Ready))]
[SpireFeature("ui.settings.tabs")]
internal static class SettingsTabsPatch
{
    private static readonly AccessTools.FieldRef<NSettingsTabManager, Dictionary<NSettingsTab, NSettingsPanel>> TabsField =
        AccessTools.FieldRefAccess<NSettingsTabManager, Dictionary<NSettingsTab, NSettingsPanel>>("_tabs");

    private static readonly Action<NSettingsTabManager, NSettingsTab> SwitchTabTo =
        AccessTools.MethodDelegate<Action<NSettingsTabManager, NSettingsTab>>(
            AccessTools.Method(typeof(NSettingsTabManager), "SwitchTabTo"));

    [HarmonyPostfix]
    private static void Postfix(NSettingsTabManager __instance)
    {
        if (SpireRuntime.Current is not { } context || context.SettingsTabs.Tabs.Count == 0)
        {
            return;
        }

        try
        {
            Inject(__instance, context);
        }
#pragma warning disable CA1031 // Never break the game's Settings screen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            context.Log.Error("Could not add Settings tabs.", ex);
        }
    }

    private static void Inject(NSettingsTabManager manager, ModContext context)
    {
        var tabs = TabsField(manager);
        var screen = manager.Owner ?? manager.GetParent();
        var general = screen.GetNodeOrNull<NSettingsPanel>("%GeneralSettings");
        if (general?.GetParent() is not { } panelParent)
        {
            context.Log.Warn("Settings screen layout changed (no General panel); mod tabs skipped.");
            return;
        }

        var templates = new SettingsTemplates(general);
        if (!templates.IsComplete)
        {
            context.Log.Warn("Settings rows will use fallback styling (game layout changed).");
        }

        var insertBefore = manager.GetNodeOrNull<Control>("%RightTabIcon");
        foreach (var handle in context.SettingsTabs.Tabs)
        {
            var definition = handle.Tab;
            var tab = GameScenes.Instantiate<NSettingsTab>(GameScenes.SettingsTab);
            tab.Name = $"Mod_{context.Id}_{definition.Title}";
            manager.AddChild(tab);
            if (insertBefore is not null)
            {
                manager.MoveChild(tab, insertBefore.GetIndex());
            }

            tab.SetLabel(definition.Title);

            var panel = new SettingsPanel { Name = $"Mod_{context.Id}_{definition.Title}_Settings" };
            panel.Configure(definition.Build, templates);
            panel.Visible = false;
            panelParent.AddChild(panel);
            handle.Track(panel);

            tabs.Add(tab, panel);
            tab.Connect(NClickableControl.SignalName.Released, Callable.From<NSettingsTab>(t => SwitchTabTo(manager, t)));
        }
    }
}
