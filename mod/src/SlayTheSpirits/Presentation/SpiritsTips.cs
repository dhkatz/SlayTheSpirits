using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Powers;
using SlayTheSpirits.Rules;
using SpireKit.Localization;

namespace SlayTheSpirits.Presentation;

/// <summary>
/// Hoverable keywords: Sip, Drink, Shot… and Tipsy. Text comes from the rules in effect, so tooltips always
/// state the host's configured labels and Tipsy values.
/// </summary>
/// <remarks>
/// The game doesn't cascade tooltips, so a card lists every tip it wants shown: its penalty keyword(s)
/// followed by Tipsy. Tips are keyed per keyword, so the same keyword on two cards shows once.
/// </remarks>
internal static class SpiritsTips
{
    private static RuleSet Rules => SlayTheSpiritsMod.Instance?.Rules.Effective ?? Presets.Standard;

    public static IHoverTip Penalty(PenaltyKind kind)
    {
        var entry = Glossary.For(Rules, kind);
        return SpireTips.Text($"SPIRITS_{kind.ToString().ToUpperInvariant()}", entry.Title, entry.Body);
    }

    public static IHoverTip Tipsy()
    {
        var entry = Glossary.Tipsy(Rules);
        return SpireTips.Text("SPIRITS_TIPSY", entry.Title, entry.Body, ModelDb.Power<SpiritsTipsyPower>().Icon);
    }

    /// <summary>Penalty keyword tips followed by the Tipsy tip.</summary>
    public static IEnumerable<IHoverTip> ForPenalties(IEnumerable<PenaltyKind> kinds)
    {
        var distinct = kinds.Distinct().ToList();
        foreach (var kind in distinct)
        {
            yield return Penalty(kind);
        }

        if (distinct.Any(k => TipsyWeights.PerUnit(Rules, k) > 0))
        {
            yield return Tipsy();
        }
    }

    /// <summary>"Tonight so far: 3 Sips, 1 Shot" for a player, or null outside a run.</summary>
    public static IHoverTip? Breakdown(PlayerId player)
    {
        if (SlayTheSpiritsMod.Game?.Tally is not { } tally)
        {
            return null;
        }

        var rules = Rules;
        var parts = Enum.GetValues<PenaltyKind>()
            .Where(k => k != PenaltyKind.Water && tally.Count(player, k) > 0)
            .Select(k =>
            {
                var count = tally.Count(player, k);
                var label = PenaltyLabels.For(rules, k);
                return count == 1 ? $"1 {label.Singular}" : $"{count} {label.Plural}";
            })
            .ToList();
        var body = parts.Count == 0 ? "Stone cold sober." : string.Join(", ", parts) + ".";
        return SpireTips.Text("SPIRITS_TONIGHT", "Tonight so far", body);
    }
}
