using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using SpireKit.Patching;

namespace SpireKit.Modding.Patches;

/// <summary>Fires <see cref="SpireMod.OnGameReady"/> after ModelDb, localization and message types are initialized.</summary>
[HarmonyPatch(typeof(OneTimeInitialization), nameof(OneTimeInitialization.ExecuteEssential))]
[SpireFeature("sdk.gameReady", Critical = true)]
internal static class GameReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix() => SpireBootstrap.NotifyGameReady();
}
