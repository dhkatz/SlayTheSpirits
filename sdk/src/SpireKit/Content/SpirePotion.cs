using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Patching;

namespace SpireKit.Content;

/// <summary>
/// Base class for mod potions. Art is one raw PNG in the mod's PCK, <c>res://&lt;ModId&gt;/images/potions/&lt;entry&gt;.png</c>
/// (128×128 works well), used for the bottle in the potion belt, the large image and, as a generated silhouette, the
/// outline behind the bottle.
/// </summary>
/// <remarks>
/// <para>
/// Register with <see cref="ContentRegistry.AddPotion{TPool,TPotion}"/>. Potions in <c>SharedPotionPool</c> drop for
/// every character; use <see cref="ContentRegistry.SetPotionFilter"/> to switch them off per run.
/// </para>
/// <para>Text lives in the <c>potions</c> table: <c>&lt;ENTRY&gt;.title</c> and <c>.description</c>.</para>
/// </remarks>
public abstract class SpirePotion : PotionModel
{
    /// <summary><c>res://&lt;ModId&gt;/images/potions/&lt;entry&gt;.png</c>.</summary>
    public string CustomImagePath => ModArt.PathFor("potions", Id);
}

/// <summary>Serves <see cref="SpirePotion"/> art. The game loads potion images from its own atlases through non-virtual getters.</summary>
[HarmonyPatch]
[SpireFeature("content.potionImages")]
internal static class PotionImagePatches
{
    [HarmonyPatch(typeof(PotionModel), nameof(PotionModel.Image), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool Image(PotionModel __instance, ref Texture2D __result) =>
        Serve(__instance is SpirePotion potion ? ModArt.Load(potion.CustomImagePath) : null, ref __result);

    [HarmonyPatch(typeof(PotionModel), nameof(PotionModel.LargeImage), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool LargeImage(PotionModel __instance, ref Texture2D __result) =>
        Serve(__instance is SpirePotion potion ? ModArt.Load(potion.CustomImagePath) : null, ref __result);

    [HarmonyPatch(typeof(PotionModel), nameof(PotionModel.Outline), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool Outline(PotionModel __instance, ref Texture2D? __result)
    {
        if (__instance is not SpirePotion potion)
        {
            return true;
        }

        // Never fall through: the game would look for an atlas entry that doesn't exist. No art means no outline.
        __result = ModArt.Outline(potion.CustomImagePath);
        return false;
    }

    private static bool Serve(Texture2D? texture, ref Texture2D result)
    {
        if (texture is null)
        {
            return true;
        }

        result = texture;
        return false;
    }
}
