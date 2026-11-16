using SlayTheSpirits.Engine;

namespace SlayTheSpirits.Tavern;

/// <summary>What the bartender says.</summary>
internal static class TavernLines
{
    public static IReadOnlyList<string> Greetings { get; } =
    [
        "Welcome to the Spirited Tap! What'll it be?",
        "Pull up a stool. First water's on the house.",
        "Ah, the living! We don't get many of you.",
    ];

    public static IReadOnlyList<string> TooPoor { get; } =
    [
        "Coin first, friend.",
        "Your purse is lighter than my soul.",
        "Come back with more gold.",
    ];

    private static readonly IReadOnlyList<string> Cheers = ["Cheers!", "Down the hatch!", "Good choice."];

    public static IReadOnlyList<string> For(TavernItem item) => item.Id switch
    {
        "house_lager" => ["One house lager, coming up!", "Brewed it myself. Two hundred years ago."],
        "the_special" => ["Don't ask what's in it.", "The Special! Feel that? That's growth."],
        "top_shelf" => ["Ooh, the good stuff.", "From my private cellar. Choose wisely."],
        "glass_of_water" => ["Hydration! Smart.", "Water. A rare order around here."],
        "buy_a_round" => ["A round for the table!", "Generous! Everybody drink!"],
        "the_bouncer" => ["Out you go!", "And stay out!"],
        _ => Cheers,
    };
}
