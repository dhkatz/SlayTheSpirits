using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Patching;

namespace SpireKit.Content;

/// <summary>
/// Base class for mod powers. Icons are read from <c>res://&lt;ModId&gt;/images/powers/&lt;entry&gt;.png</c> (raw PNG in
/// the mod's PCK) for both the small icon (power row, tooltips) and the big icon (apply/flash effects).
/// Without art the game's "missing power" icon is used.
/// </summary>
/// <remarks>
/// Powers need no pool registration; the game discovers every concrete <see cref="PowerModel"/>. Localization
/// lives in the <c>powers</c> table under <c>&lt;ENTRY&gt;.title</c> / <c>.description</c> / <c>.smartDescription</c>.
/// </remarks>
public abstract class SpirePower : PowerModel
{
    /// <summary><c>res://&lt;ModId&gt;/images/powers/&lt;entry&gt;.png</c>.</summary>
    public string CustomIconPath => ModArt.PathFor("powers", Id);
}

/// <summary>Serves custom icons for <see cref="SpirePower"/>s. Both getters are non-virtual in the game.</summary>
[HarmonyPatch]
[SpireFeature("content.powerIcons")]
internal static class PowerIconPatches
{
    [HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Icon), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool Icon(PowerModel __instance, ref Texture2D __result) => Serve(__instance, ref __result);

    [HarmonyPatch(typeof(PowerModel), nameof(PowerModel.BigIcon), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool BigIcon(PowerModel __instance, ref Texture2D __result) => Serve(__instance, ref __result);

    private static bool Serve(PowerModel power, ref Texture2D result)
    {
        if (power is SpirePower mine && ModArt.Load(mine.CustomIconPath) is { } texture)
        {
            result = texture;
            return false;
        }

        return true;
    }
}
