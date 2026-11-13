using MegaCrit.Sts2.Core.Modding;
using SpireKit.GameEvents;
using SpireKit.Modding;

namespace SlayTheSpirits;

/// <summary>The game's entry point into the mod.</summary>
[ModInitializer(nameof(Init))]
public static class Entry
{
    public static void Init() => SpireBootstrap.Boot<SlayTheSpiritsMod>();
}

/// <summary>Receives game hooks for SpireKit. Exists only to give the listener a mod-unique model id.</summary>
public sealed class SpiritsHookListener : SpireHookListener;
