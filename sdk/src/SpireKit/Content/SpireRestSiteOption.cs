using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using SpireKit.Logging;
using SpireKit.Modding;
using SpireKit.Patching;

namespace SpireKit.Content;

/// <summary>
/// A mod option at rest sites, next to Rest and Smith (like the Shovel relic's Dig). Offer it with
/// <see cref="ModContext.RestSite"/>.
/// </summary>
/// <remarks>
/// <para>
/// The game reads the option's text from the <c>rest_site_ui</c> table: <c>OPTION_&lt;OptionId&gt;.name</c>,
/// <c>.description</c> and (when <see cref="RestSiteOption.IsEnabled"/> is false) <c>.descriptionDisabled</c>.
/// Ship them in <c>localization/eng/rest_site_ui.json</c> or set them with <see cref="Localization.LocInjector"/>.
/// Prefix <see cref="RestSiteOption.OptionId"/> with the mod's model prefix so ids never collide.
/// </para>
/// <para>
/// <see cref="RestSiteOption.OnSelect"/> runs for the player who picked it; return false if they backed out
/// (e.g. cancelled a card choice) so the option isn't used up.
/// </para>
/// </remarks>
public abstract class SpireRestSiteOption(Player owner) : RestSiteOption(owner)
{
    /// <summary>Shown when the option's own icon is missing; the game requires one (hovering an option shows it in a thought bubble).</summary>
    public const string FallbackIconPath = "res://images/ui/rest_site/option_heal.png";

    /// <summary>The option's icon, a raw PNG in the mod's PCK, e.g. <c>res://MyMod/images/rest_site/my_option.png</c>.</summary>
    public abstract string IconPath { get; }

    /// <summary>
    /// Nothing to preload: the game preloads these through <c>ResourceLoader</c>, which can't read a mod's raw PNGs
    /// (the icon is decoded by <see cref="ModArt"/> instead).
    /// </summary>
    public override IEnumerable<string> AssetPaths => [];

    /// <summary>The player this option is for.</summary>
    protected Player Player => Owner;

    /// <summary>Never null: the game's rest site UI throws on a missing icon.</summary>
    internal Texture2D LoadIcon() => ModArt.Load(IconPath) ?? ResourceLoader.Load<Texture2D>(FallbackIconPath);
}

/// <summary>Rest site options a mod adds for each player.</summary>
public sealed class RestSiteOptionRegistry
{
    private readonly List<Func<Player, RestSiteOption?>> _factories = [];

    /// <summary>
    /// Adds an option per player at every rest site; return null to skip a player. Runs on every peer, so decide
    /// only from synced state (run state, host-authoritative settings).
    /// </summary>
    public void Add(Func<Player, RestSiteOption?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.Add(factory);
    }

    internal bool AddTo(Player player, ICollection<RestSiteOption> options, IModLog log)
    {
        var added = false;
        foreach (var factory in _factories)
        {
            try
            {
                if (factory(player) is { } option)
                {
                    options.Add(option);
                    added = true;
                }
            }
#pragma warning disable CA1031 // One broken option must not empty the rest site.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                log.Error("A rest site option failed to build.", ex);
            }
        }

        return added;
    }
}

/// <summary>
/// Rest site icons are looked up at a fixed path inside the game's own files (<c>images/ui/rest_site/</c>), which a
/// mod can't add to; mod options supply their own. The getter must never return null for a mod option: the button,
/// the confirmation and the hover thought bubble (local and from teammates) all use it, and the bubble throws.
/// </summary>
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Icon), MethodType.Getter)]
[SpireFeature("content.restsite.icons")]
internal static class RestSiteIconPatch
{
    [HarmonyPrefix]
    private static bool Prefix(RestSiteOption __instance, ref Texture2D __result)
    {
        if (__instance is not SpireRestSiteOption option)
        {
            return true;
        }

        __result = option.LoadIcon();
        return false;
    }
}
