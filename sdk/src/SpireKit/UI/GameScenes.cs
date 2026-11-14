using Godot;

namespace SpireKit.UI;

/// <summary>Helpers for building mod UI out of the game's own packed scenes.</summary>
public static class GameScenes
{
    /// <summary>Settings-row tickbox (root script <c>NSettingsTickbox</c>).</summary>
    public const string SettingsTickbox = "res://scenes/screens/settings_tickbox.tscn";

    /// <summary>Settings-row option picker with arrows (root script <c>NPaginator</c>).</summary>
    public const string Paginator = "res://scenes/screens/paginator.tscn";

    /// <summary>A Settings screen tab button (root script <c>NSettingsTab</c>).</summary>
    public const string SettingsTab = "res://scenes/screens/settings_tab.tscn";

    // Properties that describe the node's identity or links rather than its look and layout.
    private static readonly HashSet<string> NotCopied = new(StringComparer.Ordinal)
    {
        "script", "name", "owner", "unique_name_in_owner", "scene_file_path", "process_thread_group",
    };

    /// <summary>
    /// Gives <paramref name="target"/> the contents of a game scene: the scene's root properties (size, focus,
    /// materials, exported settings) and all its children, re-owned so <c>%UniqueName</c> lookups resolve.
    /// </summary>
    /// <remarks>
    /// This is how a mod subclasses a game widget whose parts live in a scene: write
    /// <c>partial class MyTickbox : NSettingsTickbox</c>, call <c>Graft(this, GameScenes.SettingsTickbox)</c> in the
    /// constructor, and override <c>_Ready</c> to call the widget's <c>ConnectSignals()</c> as the game's own
    /// subclasses do. Call it before <paramref name="target"/> enters the tree.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The scene can't be loaded.</exception>
    public static void Graft(Node target, string scenePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        var scene = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Scene not found: {scenePath}");
        var source = scene.Instantiate(PackedScene.GenEditState.Disabled);
        try
        {
            CopyProperties(source, target);
            foreach (var child in source.GetChildren(includeInternal: false))
            {
                source.RemoveChild(child);
                target.AddChild(child);
                Reown(child, source, target);
            }
        }
        finally
        {
            source.Free();
        }
    }

    /// <summary>
    /// Copies every saved property (layout, theme overrides, materials, exported script fields) from one node to
    /// another, except identity (name, script, owner). Properties the target doesn't have are ignored.
    /// </summary>
    public static void CopyProperties(Node from, Node to, params string[] except)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        foreach (var property in from.GetPropertyList())
        {
            var name = property["name"].AsString();
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            if (usage.HasFlag(PropertyUsageFlags.Storage) && !NotCopied.Contains(name) && !except.Contains(name))
            {
                to.Set(name, from.Get(name));
            }
        }
    }

    /// <summary>Creates a game scene's root node, e.g. a <c>NSettingsTab</c>.</summary>
    public static T Instantiate<T>(string scenePath)
        where T : Node
    {
        var scene = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Scene not found: {scenePath}");
        return scene.Instantiate<T>(PackedScene.GenEditState.Disabled);
    }

    private static void Reown(Node node, Node oldOwner, Node newOwner)
    {
        // Only nodes the scene itself owned move; nodes inside nested scene instances keep their own owner.
        if (node.Owner == oldOwner || node.Owner is null)
        {
            node.Owner = newOwner;
        }

        foreach (var child in node.GetChildren(includeInternal: false))
        {
            Reown(child, oldOwner, newOwner);
        }
    }
}
