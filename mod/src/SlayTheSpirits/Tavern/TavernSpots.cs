using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;

namespace SlayTheSpirits.Tavern;

/// <summary>Answers "is this shop the Tavern?" for the game, from the host's rules (<see cref="Taverns"/>).</summary>
internal static class TavernSpots
{
    /// <summary>
    /// Developer flag: every shop is the Tavern, solo runs included, so the dev console's <c>room shop</c> opens it.
    /// Every player in a test needs it (<c>tools/launch.ps1 -Tavern</c>, <c>tools/test.ps1 -Tavern</c>).
    /// </summary>
    public const string DevFlag = "--spirits-tavern";

    private static readonly bool Forced = OS.GetCmdlineArgs().Contains(DevFlag);

    /// <summary>Whether <paramref name="point"/> on the current act's map is the Tavern.</summary>
    public static bool IsTavern(IRunState run, MapPoint point)
    {
        if (point.PointType != MapPointType.Shop || run.Players.Count < 2 || SlayTheSpiritsMod.Instance?.Rules.Effective is not { } rules)
        {
            return false;
        }

        var shops = run.Map.GetAllMapPoints()
            .Where(static p => p.PointType == MapPointType.Shop)
            .Select(static p => new MapSpot(p.coord.col, p.coord.row))
            .ToList();
        return Taverns.IsTavern(rules, run.Rng.StringSeed, run.CurrentActIndex, shops, new MapSpot(point.coord.col, point.coord.row));
    }

    /// <summary>Whether the players are standing on the Tavern right now.</summary>
    public static bool IsAtTavern(IRunState run) => Forced || (run.CurrentMapPoint is { } point && IsTavern(run, point));
}
