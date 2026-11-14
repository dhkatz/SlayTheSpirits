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

    /// <summary>
    /// A white silhouette of the art at <paramref name="path"/>, grown by <paramref name="width"/> pixels, for the
    /// outline the game draws behind relic and potion icons (it tints it). Null if the art doesn't exist.
    /// </summary>
    public static Texture2D? Outline(string path, int width = 3)
    {
        var key = $"{path}#outline{width}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        Texture2D? texture = null;
        if (Godot.FileAccess.FileExists(path) && Image.LoadFromFile(path) is { } image && !image.IsEmpty())
        {
            texture = ImageTexture.CreateFromImage(Silhouette(image, width));
        }

        Cache[key] = texture;
        return texture;
    }

    private static Image Silhouette(Image source, int width)
    {
        source.Convert(Image.Format.Rgba8);
        int w = source.GetWidth(), h = source.GetHeight();
        var solid = new bool[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                solid[(y * w) + x] = source.GetPixel(x, y).A > 0.4f;
            }
        }

        var outline = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var radius2 = width * width;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (Near(solid, w, h, x, y, width, radius2))
                {
                    outline.SetPixel(x, y, Colors.White);
                }
            }
        }

        return outline;
    }

    private static bool Near(bool[] solid, int w, int h, int x, int y, int width, int radius2)
    {
        for (var dy = -width; dy <= width; dy++)
        {
            for (var dx = -width; dx <= width; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx * dx) + (dy * dy) <= radius2 && nx >= 0 && ny >= 0 && nx < w && ny < h && solid[(ny * w) + nx])
                {
                    return true;
                }
            }
        }

        return false;
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
