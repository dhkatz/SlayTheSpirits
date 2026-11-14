using System.Globalization;
using System.Text;
using Godot;
using SpireKit.Logging;
using SpireKit.Modding;

namespace SpireKit.UI;

/// <summary>
/// Developer tool: writes the node tree of game scenes to text, so mod UI can match layouts that only exist in the
/// game's packed <c>.tscn</c> files. Start the game with <c>--spirekit-dump-scenes res://scenes/a.tscn,res://scenes/b.tscn</c>
/// (<c>tools/launch.ps1 -DumpScenes ...</c>); output goes to the mod's storage folder under <c>dumps/</c>.
/// </summary>
public static class SceneDump
{
    /// <summary>Command-line flag listing the scenes to dump (comma-separated <c>res://</c> paths).</summary>
    public const string Flag = "--spirekit-dump-scenes";

    /// <summary>Describes <paramref name="root"/> and its descendants (internal children included), one node per line.</summary>
    public static string Describe(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var text = new StringBuilder();
        Describe(root, root, 0, text);
        return text.ToString();
    }

    internal static void RunIfRequested(ModContext context)
    {
        var args = OS.GetCmdlineArgs();
        var index = Array.IndexOf(args, Flag);
        if (index < 0 || index + 1 >= args.Length)
        {
            return;
        }

        foreach (var path in args[index + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (ResourceLoader.Load<PackedScene>(path) is not { } scene)
                {
                    context.Log.Warn($"Scene dump: '{path}' is not a scene.");
                    continue;
                }

                var root = scene.Instantiate(PackedScene.GenEditState.Disabled);
                var file = $"dumps/{Path.GetFileNameWithoutExtension(path)}.txt";
                context.Storage.WriteText(file, Describe(root));
                root.Free();
                context.Log.Info($"Scene dump: {path} -> {file}");
            }
#pragma warning disable CA1031 // Developer tool: report and carry on.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                context.Log.Error($"Scene dump of '{path}' failed.", ex);
            }
        }
    }

    private static void Describe(Node root, Node node, int depth, StringBuilder text)
    {
        text.Append(' ', depth * 2);
        if (node.UniqueNameInOwner)
        {
            text.Append('%');
        }

        text.Append(node.Name.ToString()).Append(" : ").Append(node.GetClass());
        if (node.GetType().Namespace is { } ns && !ns.StartsWith("Godot", StringComparison.Ordinal))
        {
            text.Append(" [").Append(node.GetType().Name).Append(']');
        }

        if (node.Owner != root && node != root)
        {
            text.Append(" (sub-scene");
            if (!string.IsNullOrEmpty(node.SceneFilePath))
            {
                text.Append(' ').Append(node.SceneFilePath);
            }

            text.Append(')');
        }
        else if (!string.IsNullOrEmpty(node.SceneFilePath) && node != root)
        {
            text.Append(" (instance ").Append(node.SceneFilePath).Append(')');
        }

        AppendDetails(node, text);
        text.Append('\n');
        foreach (var child in node.GetChildren(includeInternal: true))
        {
            Describe(root, child, depth + 1, text);
        }
    }

    private static void AppendDetails(Node node, StringBuilder text)
    {
        if (node is CanvasItem { Visible: false })
        {
            text.Append(" hidden");
        }

        if (node is Control control)
        {
            text.Append(CultureInfo.InvariantCulture, $" pos={Fmt(control.Position)} size={Fmt(control.Size)}");
            if (control.CustomMinimumSize != Vector2.Zero)
            {
                text.Append(CultureInfo.InvariantCulture, $" min={Fmt(control.CustomMinimumSize)}");
            }

            if (control.FocusMode != Control.FocusModeEnum.None)
            {
                text.Append(" focus=").Append(control.FocusMode);
            }

            if (control.Modulate != Colors.White)
            {
                text.Append(" modulate=#").Append(control.Modulate.ToHtml());
            }
        }

        switch (node)
        {
            case Label label when !string.IsNullOrEmpty(label.Text):
                text.Append(" text=\"").Append(Short(label.Text)).Append('"');
                break;
            case RichTextLabel rich when !string.IsNullOrEmpty(rich.Text):
                text.Append(" text=\"").Append(Short(rich.Text)).Append('"');
                break;
            case TextureRect { Texture: { } texture }:
                text.Append(" texture=").Append(texture.ResourcePath);
                break;
            case BoxContainer box:
                text.Append(" sep=").Append(box.GetThemeConstant("separation"));
                break;
        }
    }

    private static string Fmt(Vector2 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.#},{v.Y:0.#}");

    private static string Short(string s) => (s.Length > 40 ? s[..40] + "…" : s).Replace("\n", "\\n", StringComparison.Ordinal);
}
