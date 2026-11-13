using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.addons.mega_text;

namespace SpireKit.UI;

/// <summary>
/// Native-looking combat notifications built from the game's own VFX scenes.
/// Every method returns false (and does nothing) when the visual it needs isn't on screen, so callers
/// can fall back to <see cref="ToastOverlay"/>.
/// </summary>
public static class GameVfx
{
    /// <summary>Time a combat banner occupies the screen (the game's turn banner runs ~2.2s).</summary>
    private const double BannerSeconds = 2.4;

    private static double _bannerFreeAt;

    /// <summary>
    /// A speech bubble over <paramref name="speaker"/>, like character dialogue. Supports the game's BBCode
    /// effects (<c>[jitter]</c>, <c>[gold]</c>, <c>[sine]</c>…). Works for dead characters too.
    /// </summary>
    /// <param name="speaker">The creature to speak from; must be shown in the current combat.</param>
    /// <param name="bbcode">Text, BBCode allowed.</param>
    /// <param name="seconds">Display time.</param>
    /// <param name="color">Bubble color; defaults to the character's own bubble color.</param>
    public static bool SpeechBubble(Creature speaker, string bbcode, double seconds = 2.5, VfxColor? color = null)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        if (speaker.GetVfxContainer() is not { } container)
        {
            return false;
        }

        var bubbleColor = color ?? speaker.Player?.Character.SpeechBubbleColor ?? VfxColor.White;
        if (NSpeechBubbleVfx.Create(bbcode, speaker, seconds, bubbleColor) is not { } bubble)
        {
            return false;
        }

        container.AddChildSafely(bubble);
        return true;
    }

    /// <summary>
    /// A full-width combat banner in the style of the game's "Player Turn" banner, with custom text.
    /// Banners are queued so they never overlap each other.
    /// </summary>
    /// <param name="title">Big line.</param>
    /// <param name="subtitle">Smaller line underneath (optional).</param>
    /// <param name="notBeforeSeconds">Extra delay, e.g. to wait out the game's own combat-start banners.</param>
    public static bool CombatBanner(string title, string? subtitle = null, double notBeforeSeconds = 0)
    {
        if (NCombatRoom.Instance is null || Engine.GetMainLoop() is not SceneTree tree)
        {
            return false;
        }

        var now = Time.GetTicksMsec() / 1000.0;
        var start = Math.Max(now + notBeforeSeconds, _bannerFreeAt);
        _bannerFreeAt = start + BannerSeconds;
        var delay = start - now;
        if (delay <= 0.01)
        {
            ShowBanner(title, subtitle);
        }
        else
        {
            tree.CreateTimer(delay).Timeout += () => ShowBanner(title, subtitle);
        }

        return true;
    }

    private static void ShowBanner(string title, string? subtitle)
    {
        // Combat may have ended while queued.
        if (NCombatRoom.Instance is not { } room || NPlayerTurnBanner.Create(0) is not { } banner)
        {
            return;
        }

        // The banner writes its stock "Player Turn" text in _Ready; overwrite it right after.
        banner.Ready += () =>
        {
            banner.GetNodeOrNull<MegaLabel>("Label")?.SetTextAutoSize(title);
            if (banner.GetNodeOrNull<MegaLabel>("TurnNumber") is { } sub)
            {
                if (string.IsNullOrWhiteSpace(subtitle))
                {
                    sub.Visible = false;
                }
                else
                {
                    sub.SetTextAutoSize(subtitle);
                }
            }
        };
        room.AddChildSafely(banner);
    }
}
