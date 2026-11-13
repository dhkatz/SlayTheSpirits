using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using SpireKit.Modding;

namespace SpireKit.Localization;

/// <summary>
/// Keyword-style tooltips whose title and body are computed at runtime (e.g. from host settings).
/// </summary>
/// <remarks>
/// The game's tooltips are <see cref="HoverTip"/>s built from loc keys, de-duplicated by key. Each
/// <c>key</c> passed to <see cref="Text"/> gets its own <c>{Title}</c>/<c>{Body}</c> template entries in
/// <c>static_hover_tips</c> (registered automatically), so tips with different keys never collapse together,
/// and the same key shown twice (e.g. two cards both mentioning Shot) appears once.
/// </remarks>
public static class SpireTips
{
    public const string Table = "static_hover_tips";

    /// <summary>Builds a tooltip. <paramref name="body"/> may contain BBCode; <paramref name="title"/> may not.</summary>
    /// <param name="key">Unique key, conventionally <c>MODPREFIX_NAME</c> (e.g. <c>SPIRITS_SHOT</c>).</param>
    /// <param name="title">Tooltip title.</param>
    /// <param name="body">Tooltip text.</param>
    /// <param name="icon">Optional icon shown beside the title.</param>
    public static IHoverTip Text(string key, string title, string body, Texture2D? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var titleKey = key + ".title";
        var bodyKey = key + ".description";
        if (SpireRuntime.Current?.Loc is { } loc)
        {
            loc.Set(Table, titleKey, "{Title}");
            loc.Set(Table, bodyKey, "{Body}");
        }

        var titleText = new LocString(Table, titleKey);
        titleText.Add("Title", title);
        var bodyText = new LocString(Table, bodyKey);
        bodyText.Add("Body", body);
        return new HoverTip(titleText, bodyText, icon);
    }
}
