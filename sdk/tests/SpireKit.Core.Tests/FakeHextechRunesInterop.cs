using SpireKit.Tests;

namespace HextechRunes;

/// <summary>Stands in for ARAM: Mayhem's interop class, with the same name and signatures.</summary>
public static class HextechRunesInterop
{
    public static int ApiVersion => Version;

    internal static int Version { get; set; } = 2;

    internal static List<object?[]> Registered { get; } = [];

    internal static List<(Type Rune, string Key)> Labels { get; } = [];

    internal static List<(string ModId, string Key)> Sections { get; } = [];

    internal static Exception? Throw { get; set; }

    public static void RegisterPlayerRune(
        Type runeType,
        string rarity,
        string? flags,
        string? characterPool,
        int characterOrder,
        string? tagKey,
        string? assetModId,
        Func<FakePlayer, bool>? isAvailableForPlayer)
    {
        if (Throw is { } ex)
        {
            throw ex;
        }

        Registered.Add([runeType, rarity, flags, characterPool, characterOrder, tagKey, assetModId, isAvailableForPlayer]);
    }

    public static void SetPlayerRunePoolLabel(Type runeType, string poolKey) => Labels.Add((runeType, poolKey));

    public static void RegisterConfigSectionTitle(string assetModId, string titleKey) => Sections.Add((assetModId, titleKey));

    internal static void Reset()
    {
        Version = 2;
        Registered.Clear();
        Labels.Clear();
        Sections.Clear();
        Throw = null;
    }
}
