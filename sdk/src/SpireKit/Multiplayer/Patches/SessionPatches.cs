using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Multiplayer.Patches;

[HarmonyPatch(typeof(NetHostGameService), MethodType.Constructor, typeof(PeerVersionInfo))]
[SpireFeature("sdk.session.host", Critical = true)]
internal static class HostServiceCreatedPatch
{
    [HarmonyPostfix]
    private static void Postfix(NetHostGameService __instance)
    {
        if (SpireRuntime.Current is { } context)
        {
            context.Session.OnHostServiceCreated(__instance);
            context.Net.Rebind(__instance);
        }
    }
}

[HarmonyPatch(typeof(NetClientGameService), MethodType.Constructor, typeof(PeerVersionInfo))]
[SpireFeature("sdk.session.client", Critical = true)]
internal static class ClientServiceCreatedPatch
{
    [HarmonyPostfix]
    private static void Postfix(NetClientGameService __instance)
    {
        if (SpireRuntime.Current is { } context)
        {
            context.Session.OnClientServiceCreated(__instance);
            context.Net.Rebind(__instance);
        }
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
[SpireFeature("sdk.session.cleanup")]
internal static class RunCleanUpPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (SpireRuntime.Current is { } context)
        {
            context.Session.OnRunCleanedUp();
            context.Net.Rebind(context.Session.Service);
        }
    }
}
