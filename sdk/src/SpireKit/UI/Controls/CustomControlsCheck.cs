using Godot;
using SpireKit.Diagnostics;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.UI.Controls;

/// <summary>
/// Proves at startup that Godot runs this mod's own node classes: a hidden probe must get its
/// <c>_Ready</c> call and deliver a signal to C#. Reported as feature <see cref="Feature"/>.
/// </summary>
/// <remarks>
/// Mod assemblies aren't registered with Godot's script system the way the game's is; instead each class gets
/// a script on first <c>new</c>, and Godot learns its methods and signals from source-generated code. A Godot
/// or game update could change that, so check once rather than discover it as a blank settings screen.
/// </remarks>
internal static class CustomControlsCheck
{
    public const string Feature = "ui.controls";

    private const double TimeoutSeconds = 3;

    public static void Run(ModContext context)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            context.Health.Report(context.Id, Feature, FeatureStatus.Degraded, "no scene tree to check in");
            return;
        }

        var probe = new ControlsProbe { Name = "SpireKitControlsProbe", Visible = false };
        var signalled = false;
        probe.Pinged += () => signalled = true;
        tree.Root.CallDeferred(Node.MethodName.AddChild, probe);

        tree.CreateTimer(TimeoutSeconds).Timeout += () =>
        {
            var ok = probe.Readied && signalled;
            var detail = ok ? null : $"probe ready: {probe.Readied}, signal delivered: {signalled}";
            context.Health.Report(context.Id, Feature, ok ? FeatureStatus.Healthy : FeatureStatus.Disabled, detail);
            if (ok)
            {
                context.Log.Info("Custom controls: Godot runs SpireKit node classes.");
            }
            else
            {
                context.Log.Error($"Custom controls don't work in this game version ({detail}); mod UI built from them will be missing.");
            }

            if (GodotObject.IsInstanceValid(probe))
            {
                probe.QueueFree();
            }
        };
    }
}

/// <summary>The smallest possible custom node: an overridden method and a signal.</summary>
internal sealed partial class ControlsProbe : Control
{
    [Signal]
    public delegate void PingedEventHandler();

    public bool Readied { get; private set; }

    public override void _Ready()
    {
        Readied = true;
        EmitSignal(SignalName.Pinged);
    }
}
