using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Content.Patches;

/// <summary>
/// Adds the events registered with <see cref="ContentRegistry.AddEvent{TEvent}"/> to the game's shared events, the ones
/// every act draws from. The game has no pool to register events into: acts list their own, and
/// <see cref="ModelDb.AllSharedEvents"/> lists the rest.
/// </summary>
/// <remarks>
/// <see cref="ModelDb.Preload"/> reads it once model ids exist, and <see cref="ModelDb.AllEvents"/> caches the result,
/// so the events must be registered in <see cref="SpireMod.OnInitialize"/>. Each act shuffles every shared event into
/// its list at the start of a run, so adding events changes the order the game's own events come in; peers all run
/// the same mods, so they all see the same order.
/// </remarks>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
[SpireFeature("content.events")]
internal static class SharedEventsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<EventModel> __result)
    {
        if (SpireRuntime.Current?.Content is not { Events.Count: > 0 } registry)
        {
            return;
        }

        var shared = __result.ToList();
        foreach (var type in registry.Events)
        {
            var model = ModelDb.GetById<EventModel>(ModelDb.GetId(type));
            if (!shared.Contains(model))
            {
                shared.Add(model);
            }
        }

        __result = shared;
    }
}
