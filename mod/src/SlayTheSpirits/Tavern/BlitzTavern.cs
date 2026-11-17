using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SpireKit.Content;
using SpireKit.Patching;

namespace SlayTheSpirits.Tavern;

/// <summary>
/// The Tavern in Blitz: its one-line map has a single shop, so instead of taking it, the Tavern adds a floor of its own
/// (a shop the Tavern takes over, after the treasure; see <see cref="Taverns.BlitzTavernIndex{T}"/>).
/// </summary>
/// <remarks>
/// The floor is added when the map is generated, from the host's rules, so every peer builds the same map. Saved maps
/// are loaded point by point, so a saved run keeps the floors it had.
/// </remarks>
internal static class BlitzTavern
{
    /// <summary>The Tavern's row on a Blitz map (the Ancient is row 0).</summary>
    public static int Row { get; } = Taverns.BlitzTavernIndex(BlitzActMap.pointTypes, MapPointType.Treasure) + 1;

    /// <summary>The Blitz floors with the Tavern's added. Declared after <see cref="Row"/>, which it uses.</summary>
    public static IReadOnlyList<MapPointType> Floors { get; } = WithTavern(BlitzActMap.pointTypes);

    private static List<MapPointType> WithTavern(IReadOnlyList<MapPointType> floors)
    {
        var withTavern = floors.ToList();
        withTavern.Insert(Row - 1, MapPointType.Shop);
        return withTavern;
    }
}

/// <summary>Builds Blitz maps with the Tavern's floor, when the run has a Tavern.</summary>
[HarmonyPatch(typeof(BlitzActMap), MethodType.Constructor, typeof(IRunState))]
[SpireFeature("tavern.blitz")]
internal static class BlitzTavernMapPatch
{
    private static readonly AccessTools.FieldRef<BlitzActMap, MapPoint?[,]> Grid = AccessTools.FieldRefAccess<BlitzActMap, MapPoint?[,]>("<Grid>k__BackingField");
    private static readonly AccessTools.FieldRef<BlitzActMap, MapPoint> Start = AccessTools.FieldRefAccess<BlitzActMap, MapPoint>("<StartingMapPoint>k__BackingField");
    private static readonly AccessTools.FieldRef<BlitzActMap, MapPoint> Boss = AccessTools.FieldRefAccess<BlitzActMap, MapPoint>("<BossMapPoint>k__BackingField");
    private static readonly AccessTools.FieldRef<BlitzActMap, MapPoint?> SecondBoss = AccessTools.FieldRefAccess<BlitzActMap, MapPoint?>("<SecondBossMapPoint>k__BackingField");

    /// <summary>Lays the line out again the way the game does, one floor longer.</summary>
    private static void Postfix(BlitzActMap __instance, IRunState state)
    {
        if (!TavernSpots.IsOffered(state))
        {
            return;
        }

        var floors = BlitzTavern.Floors;
        var col = __instance.StartingMapPoint.coord.col;
        var grid = new MapPoint?[__instance.GetColumnCount(), floors.Count + 1];
        var start = new MapPoint(col, 0) { PointType = MapPointType.Ancient };
        var previous = start;
        for (var i = 0; i < floors.Count; i++)
        {
            var point = new MapPoint(col, i + 1) { PointType = floors[i] };
            grid[col, i + 1] = point;
            previous.AddChildPoint(point);
            previous = point;
        }

        var boss = new MapPoint(col, floors.Count + 1) { PointType = MapPointType.Boss };
        previous.AddChildPoint(boss);
        MapPoint? secondBoss = null;
        if (__instance.SecondBossMapPoint is not null)
        {
            secondBoss = new MapPoint(col, floors.Count + 2) { PointType = MapPointType.Boss };
            boss.AddChildPoint(secondBoss);
        }

        Grid(__instance) = grid;
        Start(__instance) = start;
        Boss(__instance) = boss;
        SecondBoss(__instance) = secondBoss;
    }
}

/// <summary>
/// Some relics stop dropping late in act 3, which the game counts in floors from Blitz's layout; with the Tavern's
/// floor in every act, the count moves with it.
/// </summary>
[HarmonyPatch(typeof(RelicModel), "IsLateInAct3")]
[SpireFeature("tavern.blitz")]
internal static class BlitzTavernLateInActPatch
{
    private static void Postfix(IRunState runState, ref bool __result)
    {
        if (runState.GameMode != GameMode.Blitz || !TavernSpots.IsOffered(runState))
        {
            return;
        }

        // The game's formula: two whole acts (each with its Ancient and boss), then the act 3 elite.
        var floors = BlitzTavern.Floors;
        var actLength = floors.Count + 2;
        __result = runState.TotalFloor >= actLength * 2 + floors.ToList().IndexOf(MapPointType.Elite) + 2;
    }
}

/// <summary>The Tavern's floor shows the tankard in Blitz's map along the top bar, too.</summary>
[HarmonyPatch(typeof(NBlitzTopBarMapPoint), "UpdateIcon")]
[SpireFeature("tavern.mapIcon")]
internal static class BlitzTavernTopBarIconPatch
{
    private static readonly AccessTools.FieldRef<NBlitzTopBarMapPoint, TextureRect> Icon = AccessTools.FieldRefAccess<NBlitzTopBarMapPoint, TextureRect>("_icon");
    private static readonly AccessTools.FieldRef<NBlitzTopBarMapPoint, TextureRect> Outline = AccessTools.FieldRefAccess<NBlitzTopBarMapPoint, TextureRect>("_outline");
    private static readonly AccessTools.FieldRef<NBlitzTopBarMapPoint, IRunState> Run = AccessTools.FieldRefAccess<NBlitzTopBarMapPoint, IRunState>("_runState");

    private static void Postfix(NBlitzTopBarMapPoint __instance)
    {
        if (Run(__instance) is not { } run || !TavernSpots.IsTavern(run, __instance.Point))
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
