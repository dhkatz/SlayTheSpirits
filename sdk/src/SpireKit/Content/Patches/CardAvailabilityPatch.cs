using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Content.Patches;

/// <summary>Applies <see cref="ContentRegistry.SetCardFilter"/> to every in-run card source.</summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
[SpireFeature("content.cardFilter")]
internal static class CardAvailabilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<CardModel> __result, CardMultiplayerConstraint multiplayerConstraint)
    {
        // Runs always pass Singleplayer/MultiplayerOnly; None means the card library, which shows everything.
        if (multiplayerConstraint == CardMultiplayerConstraint.None
            || SpireRuntime.Current?.Content is not { HasCardFilter: true } registry)
        {
            return;
        }

        __result = __result.Where(registry.IsCardAllowed).ToList();
    }
}
