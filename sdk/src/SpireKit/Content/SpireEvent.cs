using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SpireKit.Patching;

namespace SpireKit.Content;

/// <summary>
/// Base class for mod events: a "?" room with a portrait, a description and options, using the game's default event
/// layout. Register with <see cref="ContentRegistry.AddEvent{TEvent}"/>; it joins the game's shared events, which can
/// turn up in any act. Override <see cref="EventModel.IsAllowed"/> to restrict when (the game skips events that
/// aren't allowed when it picks the next one, so the answer must be the same on every peer).
/// </summary>
/// <remarks>
/// <para>
/// The portrait is one raw PNG in the mod's PCK, <c>res://&lt;ModId&gt;/images/events/&lt;entry&gt;.png</c>; it's
/// required, since the game has no "missing" portrait to fall back on.
/// </para>
/// <para>
/// Text lives in the <c>events</c> table: <c>&lt;ENTRY&gt;.title</c>, <c>&lt;ENTRY&gt;.pages.INITIAL.description</c>,
/// then <c>&lt;ENTRY&gt;.pages.&lt;PAGE&gt;.options.&lt;OPTION&gt;.title</c> / <c>.description</c> for each option
/// (<c>EventOption</c>'s text key is <c>&lt;ENTRY&gt;.pages.&lt;PAGE&gt;.options.&lt;OPTION&gt;</c>) and
/// <c>&lt;ENTRY&gt;.pages.&lt;PAGE&gt;.description</c> for each page an option leads to (<c>SetEventFinished</c>).
/// </para>
/// <para>
/// Events aren't shared by default: in multiplayer each player gets their own copy, seeded per player, and the game
/// runs every player's choices on every peer.
/// </para>
/// </remarks>
public abstract class SpireEvent : EventModel
{
    private const string DefaultLayoutScene = "res://scenes/events/default_event_layout.tscn";

    /// <summary><c>res://&lt;ModId&gt;/images/events/&lt;entry&gt;.png</c>.</summary>
    public string CustomPortraitPath => ModArt.PathFor("events", Id);

    /// <summary>Mod events use the default layout (a portrait, text and options).</summary>
    public sealed override EventLayoutType LayoutType => EventLayoutType.Default;

    /// <summary>
    /// Only the layout: the game would also preload its own <c>images/events/&lt;entry&gt;.png</c>, which a mod
    /// doesn't have (the portrait comes from <see cref="CustomPortraitPath"/>).
    /// </summary>
    public override IEnumerable<string> GetAssetPaths(IRunState runState) => [DefaultLayoutScene];
}

/// <summary>Serves <see cref="SpireEvent"/> portraits. The game loads them from its own files through a non-virtual method.</summary>
[HarmonyPatch]
[SpireFeature("content.eventPortraits")]
internal static class EventPortraitPatches
{
    [HarmonyPatch(typeof(EventModel), nameof(EventModel.CreateInitialPortrait))]
    [HarmonyPrefix]
    private static bool Portrait(EventModel __instance, ref Texture2D __result)
    {
        if (__instance is SpireEvent mine && ModArt.Load(mine.CustomPortraitPath) is { } texture)
        {
            __result = texture;
            return false;
        }

        return true;
    }
}
