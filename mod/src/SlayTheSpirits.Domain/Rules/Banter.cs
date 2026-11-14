using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>
/// Things characters say. The player handing out a drink says a <see cref="GiverLine"/>; the player drinking
/// says a <see cref="DrinkerLine"/> when their drink card is played.
/// </summary>
/// <remarks>Presentation only: lines may differ between peers, so pass any <see cref="Random"/>.</remarks>
public static class Banter
{
    private static readonly string[] Giving =
    [
        "This one's on me!",
        "Drink up, {0}!",
        "Cheers, {0}!",
        "Your round, {0}!",
        "Go on, {0}...",
        "Bottoms up, {0}!",
        "{0}, you look thirsty.",
        "Don't leave me hanging, {0}!",
    ];

    private static readonly string[] GivingEveryone =
    [
        "Drinks for everyone!",
        "A round for the table!",
        "Everybody drinks!",
        "To the Spire!",
        "Cheers, everyone!",
    ];

    private static readonly Dictionary<PenaltyKind, string[]> Drinking = new()
    {
        [PenaltyKind.Water] = ["Ahh, refreshing.", "Hydration check!", "Water? Fine..."],
        [PenaltyKind.Sip] = ["*sip*", "Just a little one.", "Mmm.", "Don't mind if I do.", "Cheers!"],
        [PenaltyKind.Finish] = ["Bottoms up!", "Gone. All gone.", "Down the hatch!", "Another!"],
        [PenaltyKind.Chug] = ["CHUG! CHUG! CHUG!", "Hold my sword...", "*glug glug glug*", "Watch and learn."],
        [PenaltyKind.Shot] = ["Here goes nothing!", "SHOT!", "Oof. That burns.", "One... two... GO!"],
    };

    /// <summary>What a player says when handing <paramref name="target"/> a drink (null = everyone).</summary>
    public static string GiverLine(Random random, string? target)
    {
        ArgumentNullException.ThrowIfNull(random);
        return target is null
            ? GivingEveryone[random.Next(GivingEveryone.Length)]
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, Giving[random.Next(Giving.Length)], target);
    }

    /// <summary>What a player says while drinking.</summary>
    public static string DrinkerLine(Random random, PenaltyKind kind)
    {
        ArgumentNullException.ThrowIfNull(random);
        var lines = Drinking[kind];
        return lines[random.Next(lines.Length)];
    }
}
