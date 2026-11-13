using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Content;

/// <summary>
/// Loads raw PNG art from a mod's PCK. Godot's <c>ResourceLoader</c> only understands imported resources,
/// so mod art is decoded with <see cref="Image.LoadFromFile"/> and cached as an <see cref="ImageTexture"/>.
/// </summary>
/// <remarks>Conventions: <c>res://&lt;ModId&gt;/images/&lt;kind&gt;/&lt;entry&gt;.png</c>, entry lowercased.</remarks>
public static class ModArt
{
    private static readonly Dictionary<string, Texture2D?> Cache = new(StringComparer.Ordinal);

    /// <summary><c>res://&lt;ModId&gt;/images/&lt;kind&gt;/&lt;entry&gt;.png</c>.</summary>
    public static string PathFor(string kind, ModelId id) =>
        $"res://{SpireRuntime.Current?.Id ?? "unbooted"}/images/{kind}/{id.Entry.ToLowerInvariant()}.png";

    /// <summary>The texture at <paramref name="path"/>, or null if the file doesn't exist.</summary>
    public static Texture2D? Load(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Texture2D? texture = null;
        if (Godot.FileAccess.FileExists(path))
        {
            var image = Image.LoadFromFile(path);
            if (image is not null && !image.IsEmpty())
            {
                texture = ImageTexture.CreateFromImage(image);
            }
        }

        Cache[path] = texture;
        return texture;
    }
}

/// <summary>Serves custom art for <see cref="SpireCard"/>s (the game would try to load it as an imported resource).</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Portrait), MethodType.Getter)]
[SpireFeature("content.cardArt")]
internal static class CardPortraitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CardModel __instance, ref Texture2D __result)
    {
        if (__instance is SpireCard card && ModArt.Load(card.CustomPortraitPath) is { } texture)
        {
            __result = texture;
            return false;
        }

        return true;
    }
}
