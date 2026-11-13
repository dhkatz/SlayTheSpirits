using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using SpireKit.Patching;

namespace SpireKit.GameEvents.Patches;

/// <summary>Remembers whether the next <c>RunManager.RunStarted</c> is a new or a loaded run.</summary>
[HarmonyPatch]
[SpireFeature("events.runStartKind.new")]
internal static class NewRunSetUpPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer)),
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer)),
    ];

    [HarmonyPrefix]
    private static void Prefix() => GameEventBridge.Instance?.SetPendingStartKind(RunStartKind.New);
}

[HarmonyPatch]
[SpireFeature("events.runStartKind.saved")]
internal static class SavedRunSetUpPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer)),
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer)),
    ];

    [HarmonyPrefix]
    private static void Prefix() => GameEventBridge.Instance?.SetPendingStartKind(RunStartKind.Loaded);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
[SpireFeature("events.runEnded")]
internal static class RunEndedPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunManager __instance, bool isVictory) =>
        GameEventBridge.Instance?.OnRunEnded(isVictory, __instance.IsAbandoned);
}
