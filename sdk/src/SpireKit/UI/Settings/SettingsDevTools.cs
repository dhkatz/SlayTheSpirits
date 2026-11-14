using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.UI.Settings;

/// <summary>
/// Developer tool: <c>--spirekit-open-settings &lt;tab title&gt;</c> opens the Settings screen on that tab as soon as the
/// main menu appears (<c>tools/launch.ps1 -OpenSettings Spirits</c>), for checking a tab without clicking. Game tabs
/// work too (<c>General</c>, <c>Graphics</c>, ...).
/// </summary>
internal static class SettingsDevTools
{
    public const string Flag = "--spirekit-open-settings";

    /// <summary>Optional: mouse-wheel ticks to scroll down after opening (to inspect the bottom of a long tab).</summary>
    public const string ScrollFlag = "--spirekit-scroll";

    private const int MaxAttempts = 120;
    private const double PreloadGraceSeconds = 6;

    public static void RunIfRequested(ModContext context)
    {
        var args = OS.GetCmdlineArgs();
        var index = Array.IndexOf(args, Flag);
        if (index < 0 || index + 1 >= args.Length || Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        var title = args[index + 1];
        var attempts = 0;
        void Poll()
        {
            if (NGame.Instance?.MainMenu is { } menu && menu.IsNodeReady())
            {
                // The game keeps preloading common assets in the background after the menu appears; opening
                // Settings during that makes Godot report "Invalid Task ID". A player can't click that fast.
                tree.CreateTimer(PreloadGraceSeconds).Timeout += () =>
                {
                    menu.OpenSettingsMenu();
                    Callable.From(() => SelectTab(context, title)).CallDeferred();
                };
                return;
            }

            if (++attempts < MaxAttempts)
            {
                tree.CreateTimer(0.5).Timeout += Poll;
            }
        }

        tree.CreateTimer(1.0).Timeout += Poll;
    }

    private static void ScrollIfRequested(Control screen)
    {
        var args = OS.GetCmdlineArgs();
        var index = Array.IndexOf(args, ScrollFlag);
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out var ticks) || screen.GetTree() is not { } tree)
        {
            return;
        }

        tree.CreateTimer(1.0).Timeout += () =>
        {
            var center = screen.GetViewportRect().Size / 2;
            for (var i = 0; i < ticks; i++)
            {
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Position = center, GlobalPosition = center });
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = false, Position = center, GlobalPosition = center });
            }
        };
    }

    private static void SelectTab(ModContext context, string title)
    {
        var tabName = $"Mod_{context.Id}_{title}";
        if (NGame.Instance?.MainMenu?.SubmenuStack.GetSubmenuType<NSettingsScreen>() is { } screen
            && (screen.FindChild(tabName, recursive: true, owned: false) ?? screen.FindChild(title, recursive: true, owned: false)) is NSettingsTab tab)
        {
            tab.ForceTabPressed();
            context.Log.Info($"Opened Settings on tab '{title}'.");
            ScrollIfRequested(screen);
        }
        else
        {
            context.Log.Warn($"Settings tab '{title}' not found.");
        }
    }
}
