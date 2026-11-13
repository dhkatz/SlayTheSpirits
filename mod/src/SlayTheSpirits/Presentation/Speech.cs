using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SpireKit.UI;

namespace SlayTheSpirits.Presentation;

/// <summary>Character speech bubbles for the drinking game. Presentation only.</summary>
internal static class Speech
{
    private static readonly Random Random = new();

    /// <summary><paramref name="giver"/> hands a drink to <paramref name="target"/> (null = the whole table).</summary>
    public static void Giver(Player giver, Player? target) =>
        GameVfx.SpeechBubble(giver.Creature, $"[gold]{Banter.GiverLine(Random, target?.Creature.Name)}[/gold]", seconds: 2.2);

    /// <summary>The drinker's reaction, styled by how heavy the drink is.</summary>
    public static string DrinkerLine(PenaltyKind kind)
    {
        var line = Banter.DrinkerLine(Random, kind);
        return kind switch
        {
            PenaltyKind.Water => $"[blue]{line}[/blue]",
            PenaltyKind.Sip => $"[gold]{line}[/gold]",
            PenaltyKind.Drink => $"[sine][gold]{line}[/gold][/sine]",
            PenaltyKind.Shot => $"[jitter][orange]{line}[/orange][/jitter]",
            _ => $"[jitter][red]{line}[/red][/jitter]",
        };
    }
}
