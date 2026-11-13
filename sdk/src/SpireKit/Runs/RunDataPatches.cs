using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using SpireKit.Logging;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Runs;

/// <summary>
/// Moves <see cref="ModContext.RunData"/> along with the game's run save: captured when the game snapshots the
/// run, written beside the save file, read back with it, appended to the save the host sends to clients, and
/// restored when a saved run is set up.
/// </summary>
internal static class RunDataBridge
{
    private const string Folder = "rundata";

    public static RunDataRegistry? Registry =>
        SpireRuntime.Current is { RunData.HasSlots: true } context ? context.RunData : null;

    public static RunIdentity? IdentityOf(SerializableRun save) =>
        save.SerializableRng?.Seed is { Length: > 0 } seed ? new RunIdentity(seed, save.StartTime) : null;

    /// <summary>Mirrors the game's save slots (<c>current_run.save</c> / <c>current_run_mp.save</c>) per profile.</summary>
    public static string? FileFor(bool multiplayer)
    {
        try
        {
            var profile = SaveManager.Instance.CurrentProfileId;
            return $"{Folder}/profile{profile}/{(multiplayer ? "current_run_mp" : "current_run")}.json";
        }
#pragma warning disable CA1031 // No profile yet: nothing to mirror.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    public static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031 // Never break the game's save/load because of mod data.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            SpireRuntime.Current?.Log.Error($"Run data: {what} failed.", ex);
        }
    }
}

/// <summary>The game snapshots the run (save, run end, replays): capture mod data for the same moment.</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
[SpireFeature("runs.data.capture")]
internal static class RunDataCapturePatch
{
    [HarmonyPostfix]
    private static void Postfix(SerializableRun __result) => RunDataBridge.Guard("capture", () =>
    {
        if (RunDataBridge.Registry is { } registry && RunDataBridge.IdentityOf(__result) is { } run)
        {
            registry.Capture(run);
        }
    });
}

/// <summary>The game writes its save file: write ours beside it.</summary>
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
[SpireFeature("runs.data.write")]
internal static class RunDataWritePatch
{
    [HarmonyPrefix]
    private static void Prefix(SerializableRun save, bool isMultiplayer) => RunDataBridge.Guard("write", () =>
    {
        if (RunDataBridge.Registry is { } registry
            && RunDataBridge.IdentityOf(save) is { } run
            && registry.Find(run) is { } bundle
            && RunDataBridge.FileFor(isMultiplayer) is { } file)
        {
            SpireRuntime.Current!.Storage.WriteText(file, bundle.Encode());
        }
    });
}

/// <summary>The game reads its save file: read ours, keeping it only if it belongs to the same run.</summary>
[HarmonyPatch]
[SpireFeature("runs.data.read")]
internal static class RunDataReadPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave)),
        AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave)),
    ];

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod, ReadSaveResult<SerializableRun> __result) => RunDataBridge.Guard("read", () =>
    {
        var multiplayer = __originalMethod.Name == nameof(RunSaveManager.LoadMultiplayerRunSave);
        if (RunDataBridge.Registry is not { } registry
            || __result is not { Success: true, SaveData: { } save }
            || RunDataBridge.IdentityOf(save) is not { } run
            || RunDataBridge.FileFor(multiplayer) is not { } file)
        {
            return;
        }

        var bundle = RunDataBundle.TryDecode(SpireRuntime.Current!.Storage.ReadText(file));
        if (bundle is null)
        {
            return;
        }

        if (bundle.Run == run)
        {
            registry.Remember(bundle);
        }
        else
        {
            SpireRuntime.Current.Log.Warn($"Run data in {file} belongs to run {bundle.Run}, not {run}; ignoring it.");
        }
    });
}

/// <summary>The game deletes its save (run over, abandoned): delete ours.</summary>
[HarmonyPatch]
[SpireFeature("runs.data.delete")]
internal static class RunDataDeletePatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.DeleteCurrentRun)),
        AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.DeleteCurrentMultiplayerRun)),
    ];

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod) => RunDataBridge.Guard("delete", () =>
    {
        var multiplayer = __originalMethod.Name == nameof(RunSaveManager.DeleteCurrentMultiplayerRun);
        if (RunDataBridge.Registry is not null && RunDataBridge.FileFor(multiplayer) is { } file)
        {
            SpireRuntime.Current!.Storage.Delete(file);
        }
    });
}

/// <summary>
/// The host sends a saved run to clients (loading a multiplayer save) as a <see cref="SerializableRun"/> packet:
/// append the bundle so clients restore the same state before the run starts.
/// </summary>
/// <remarks>
/// Both directions live in one class, and nothing is written unless both are patched, so a half-applied patch
/// can't corrupt the stream. Peers run identical mods (the game enforces it), so they agree on the format.
/// </remarks>
[HarmonyPatch]
[SpireFeature("runs.data.wire")]
internal static class RunDataWirePatch
{
    private static readonly MethodInfo SerializeMethod =
        AccessTools.Method(typeof(SerializableRun), nameof(SerializableRun.Serialize));

    private static readonly MethodInfo DeserializeMethod =
        AccessTools.Method(typeof(SerializableRun), nameof(SerializableRun.Deserialize));

    private static bool? _ready;

    private static bool Ready => _ready ??= SpireRuntime.Current is { } context
        && IsPatchedBy(SerializeMethod, context.Harmony.Id)
        && IsPatchedBy(DeserializeMethod, context.Harmony.Id);

    private static IEnumerable<MethodBase> TargetMethods() => [SerializeMethod, DeserializeMethod];

    [HarmonyPostfix]
    private static void Postfix(SerializableRun __instance, object[] __args)
    {
        if (!Ready || SpireRuntime.Current is not { } context)
        {
            return;
        }

        switch (__args[0])
        {
            case PacketWriter writer:
                // Always write (possibly empty) so the reader consumes exactly what was written.
                var bundle = RunDataBridge.Registry is { } registry && RunDataBridge.IdentityOf(__instance) is { } run
                    ? registry.Find(run)
                    : null;
                writer.WriteString(bundle?.Encode() ?? string.Empty);
                break;
            case PacketReader reader:
                var text = reader.ReadString();
                RunDataBridge.Guard("receive", () =>
                {
                    if (RunDataBundle.TryDecode(text) is { } received)
                    {
                        context.RunData.Remember(received);
                    }
                });
                break;
        }
    }

    private static bool IsPatchedBy(MethodBase method, string owner) =>
        Harmony.GetPatchInfo(method)?.Postfixes.Any(p => p.owner == owner) == true;
}

/// <summary>A saved run is being set up (single player, or a multiplayer save on every peer): restore mod data.</summary>
[HarmonyPatch]
[SpireFeature("runs.data.restore")]
internal static class RunDataRestorePatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
    [
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer)),
        AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer)),
    ];

    [HarmonyPrefix]
    private static void Prefix(object[] __args) => RunDataBridge.Guard("restore", () =>
    {
        var save = __args[1] switch
        {
            SerializableRun run => run,
            LoadRunLobby lobby => lobby.Run,
            _ => null,
        };

        if (RunDataBridge.Registry is { } registry && save is not null && RunDataBridge.IdentityOf(save) is { } identity)
        {
            registry.Restore(identity);
        }
    });
}
