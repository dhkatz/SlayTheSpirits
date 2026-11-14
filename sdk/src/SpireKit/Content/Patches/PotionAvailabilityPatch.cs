using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Content.Patches;

/// <summary>
/// Applies <see cref="ContentRegistry.SetPotionFilter"/> to every in-run potion source. Rewards, shops, events and
/// potion generators all read <c>PotionPoolModel.GetUnlockedPotions</c> (directly or through <c>PotionFactory</c>).
/// </summary>
/// <remarks>
/// Patches the base implementation, which covers every pool that doesn't override it, including the shared pool
/// mods add to. Outside a run (the potion lab, unlock tracking) everything stays visible.
/// </remarks>
[HarmonyPatch(typeof(PotionPoolModel), nameof(PotionPoolModel.GetUnlockedPotions))]
[SpireFeature("content.potionFilter")]
internal static class PotionAvailabilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<PotionModel> __result)
    {
        if (!RunManager.Instance.IsInProgress || SpireRuntime.Current?.Content is not { HasPotionFilter: true } registry)
        {
            return;
        }

        __result = __result.Where(registry.IsPotionAllowed).ToList();
    }
}
