using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Rules;

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

    /// <summary>
    /// Whether this run has a Tavern: the host's rules offer one and it's multiplayer. Rules are fixed for a run, so
    /// this doesn't change between acts.
    /// </summary>
    public static bool IsOffered(IRunState run) => RulesFor(run) is not null;

    /// <summary>Whether <paramref name="point"/> on the current act's map is the Tavern.</summary>
    public static bool IsTavern(IRunState run, MapPoint point)
    {
        if (point.PointType != MapPointType.Shop || RulesFor(run) is not { } rules)
        {
            return false;
        }

        // Blitz's own shop stays a shop; the Tavern is the floor added for it.
        if (run.GameMode == GameMode.Blitz)
        {
            return point.coord.row == BlitzTavern.Row;
        }

        var shops = run.Map.GetAllMapPoints()
            .Where(static p => p.PointType == MapPointType.Shop)
            .Select(static p => new MapSpot(p.coord.col, p.coord.row))
            .ToList();
        return Taverns.IsTavern(rules, run.Rng.StringSeed, run.CurrentActIndex, shops, new MapSpot(point.coord.col, point.coord.row));
    }

    private static RuleSet? RulesFor(IRunState run) =>
        run.Players.Count > 1 && SlayTheSpiritsMod.Instance?.Rules.Effective is { } rules && Taverns.IsOffered(rules) ? rules : null;

    /// <summary>Whether the players are standing on the Tavern right now.</summary>
    public static bool IsAtTavern(IRunState run) => Forced || (run.CurrentMapPoint is { } point && IsTavern(run, point));
}
