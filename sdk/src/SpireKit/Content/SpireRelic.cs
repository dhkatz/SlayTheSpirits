using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Patching;

namespace SpireKit.Content;

/// <summary>
/// Base class for mod relics. Art is one raw PNG in the mod's PCK, <c>res://&lt;ModId&gt;/images/relics/&lt;entry&gt;.png</c>
/// (128×128 works well; the game scales it), used for the small icon, the big icon and, as a generated silhouette,
/// the outline behind the small icon. Without art the game's "missing" icon is used.
/// </summary>
/// <remarks>
/// <para>
/// Register with <see cref="ContentRegistry.AddRelic{TPool,TRelic}"/>. Relics in the shared or character pools drop
/// as normal rewards (rarities Common, Uncommon, Rare, Shop); relics in <c>EventRelicPool</c> never drop on their own
/// and are only handed out by code (<c>RelicCmd.Obtain</c>).
/// </para>
/// <para>
/// Text lives in the <c>relics</c> table: <c>&lt;ENTRY&gt;.title</c>, <c>.description</c> and <c>.flavor</c>.
/// </para>
/// </remarks>
public abstract class SpireRelic : RelicModel
{
    /// <summary><c>res://&lt;ModId&gt;/images/relics/&lt;entry&gt;.png</c>.</summary>
    public string CustomIconPath => ModArt.PathFor("relics", Id);
}

/// <summary>Serves <see cref="SpireRelic"/> art. The game loads relic icons from its own atlases through non-virtual getters.</summary>
[HarmonyPatch]
[SpireFeature("content.relicIcons")]
internal static class RelicIconPatches
{
    [HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Icon), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool Icon(RelicModel __instance, ref Texture2D __result) =>
        Serve(__instance is SpireRelic relic ? ModArt.Load(relic.CustomIconPath) : null, ref __result);

    [HarmonyPatch(typeof(RelicModel), nameof(RelicModel.BigIcon), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool BigIcon(RelicModel __instance, ref Texture2D __result) =>
        Serve(__instance is SpireRelic relic ? ModArt.Load(relic.CustomIconPath) : null, ref __result);

    [HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IconOutline), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool IconOutline(RelicModel __instance, ref Texture2D __result) =>
        Serve(__instance is SpireRelic relic ? ModArt.Outline(relic.CustomIconPath) : null, ref __result);

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
