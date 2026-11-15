using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using SpireKit.Content;
using SpireKit.Patching;

namespace SlayTheSpirits.Tavern;

/// <summary>
/// Entering a shop that is the Tavern (<see cref="TavernSpots"/>): the room stays the game's <see cref="MerchantRoom"/>
/// (map, saves, music and shop hooks are untouched), but it gets the Tavern's menu as its stock and the
/// <see cref="NTavernRoom"/> screen instead of the merchant.
/// </summary>
internal static class TavernRoom
{
    public const string ArtDir = "res://SlayTheSpirits/images/tavern/";

    public static async Task EnterAsync(IRunState run, MerchantRoom room)
    {
        // Every peer builds every player's menu the same way, in seat order; the game's shop sync then runs each
        // purchase everywhere by entry id.
        var shop = RunManager.Instance.MerchantSynchronizer;
        shop.OnMerchantEntered([]);
        foreach (var player in run.Players)
        {
            var inventory = new MerchantInventory(player, [], [], [], [], null, []);
            inventory.testEntries.AddRange(TavernEntry.MenuFor(player));
            inventory.Setup();
            shop.SetMerchantInventory(player, inventory);
        }

        if (!TestMode.IsOn)
        {
            NRun.Instance?.SetCurrentRoom(NTavernRoom.Create(run));
        }

        await Hook.AfterRoomEntered(run, room);
    }
}

[HarmonyPatch(typeof(MerchantRoom), nameof(MerchantRoom.EnterInternal))]
[SpireFeature("tavern.room")]
internal static class TavernRoomPatch
{
    private static bool Prefix(MerchantRoom __instance, IRunState? runState, bool isRestoringRoomStackBase, ref Task __result)
    {
        if (isRestoringRoomStackBase || runState is null || !TavernSpots.IsAtTavern(runState))
        {
            return true;
        }

        __result = TavernRoom.EnterAsync(runState, __instance);
        return false;
    }
}

/// <summary>The Tavern's shop shows the tankard on the map instead of the merchant's purse.</summary>
[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
[SpireFeature("tavern.mapIcon")]
internal static class TavernMapIconPatch
{
    private static readonly AccessTools.FieldRef<NNormalMapPoint, TextureRect> Icon = AccessTools.FieldRefAccess<NNormalMapPoint, TextureRect>("_icon");
    private static readonly AccessTools.FieldRef<NNormalMapPoint, TextureRect> Outline = AccessTools.FieldRefAccess<NNormalMapPoint, TextureRect>("_outline");
    private static readonly AccessTools.FieldRef<NNormalMapPoint, IRunState> Run = AccessTools.FieldRefAccess<NNormalMapPoint, IRunState>("_runState");

    private static void Postfix(NNormalMapPoint __instance)
    {
        if (__instance.Point is not { PointType: MapPointType.Shop } point || Run(__instance) is not { } run || !TavernSpots.IsTavern(run, point))
        {
            return;
        }

        if (ModArt.Load(TavernRoom.ArtDir + "tavern_map.png") is { } icon && ModArt.Load(TavernRoom.ArtDir + "tavern_map_outline.png") is { } outline)
        {
            Icon(__instance).Texture = icon;
            Outline(__instance).Texture = outline;
        }
    }
}
