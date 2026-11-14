using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Game;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using SpireKit.UI;

namespace SlayTheSpirits.Presentation;

/// <summary>
/// Shows penalties in the game's own visual language:
/// <list type="bullet">
/// <item>In combat: a speech bubble over each drinking character, plus a turn-style banner when the whole
/// table drinks.</item>
/// <item>Otherwise (map, shops, events, run end): a banner-styled toast.</item>
/// </list>
/// Presentation only; never affects game state.
/// </summary>
internal sealed class Announcer(ToastOverlay toasts, Func<RuleSet?> rules)
{
    /// <summary>The game's own "Battle Start" + "Player Turn" banners take about this long.</summary>
    private const double CombatStartBannerDelay = 5.0;

    private static readonly Dictionary<string, string> CauseNames = new(StringComparer.Ordinal)
    {
        [BuiltInTriggers.BossEncounterStarted.Id.Value] = "Boss fight",
        [BuiltInTriggers.BossDefeated.Id.Value] = "Boss down",
        [BuiltInTriggers.EliteEncounterStarted.Id.Value] = "Elite fight",
        [BuiltInTriggers.EnemyEncountered.Id.Value] = "Spotted",
        [BuiltInTriggers.PlayerDied.Id.Value] = "Died",
        [BuiltInTriggers.PlayerBigHit.Id.Value] = "Big hit",
        [BuiltInTriggers.CurseObtained.Id.Value] = "Cursed",
        [BuiltInTriggers.PotionUsed.Id.Value] = "Potion",
        [BuiltInTriggers.TurnEndedWithEnergy.Id.Value] = "Wasted energy",
        [BuiltInTriggers.RestSiteEntered.Id.Value] = "Hydration break",
        [BuiltInTriggers.RunWon.Id.Value] = "VICTORY",
        [BuiltInTriggers.RunLost.Id.Value] = "Defeat",
    };

    public void Announce(IRunState run, IReadOnlyList<PenaltyAssignment> assignments, PartySnapshot party, CombatPhase phase)
    {
        if (assignments.Count == 0 || rules() is not { } ruleSet)
        {
            return;
        }

        foreach (var group in assignments.GroupBy(static a => a.Penalty))
        {
            var batch = group.ToList();
            var everyone = party.Members.Count > 1 && batch.Select(static a => a.Player).Distinct().Count() == party.Members.Count;
            var what = PenaltyLabels.Describe(ruleSet, group.Key).ToUpper(CultureInfo.CurrentCulture);
            var why = Describe(batch[0], ruleSet, party);

            var bubbled = phase != CombatPhase.None && batch.All(a => Bubble(run, a, ruleSet, party));
            if (bubbled && everyone)
            {
                GameVfx.CombatBanner($"EVERYONE: {what}", why, phase == CombatPhase.Starting ? CombatStartBannerDelay : 0);
            }
            else if (!bubbled)
            {
                var who = everyone
                    ? "EVERYONE"
                    : string.Join(" & ", batch.Select(a => party.Find(a.Player)?.DisplayName ?? "?").Distinct());
                toasts.Show(new Toast($"{who}: {what}", why, AccentFor(group.Key.Kind)));
            }
        }
    }

    /// <summary>Confirms at a glance that the game is on (and whose rules apply).</summary>
    public void AnnounceRunStart(RuleSet? ruleSet) => toasts.Show(ruleSet is null
        ? new Toast("SLAY THE SPIRITS", "Waiting for the host's rules…", ToastOverlay.Cream)
        : new Toast("SLAY THE SPIRITS", ruleSet.Enabled ? $"Playing '{ruleSet.Name}' rules. Drink responsibly-ish." : $"'{ruleSet.Name}' rules loaded, but penalties are OFF.", ToastOverlay.Gold, Seconds: 6));

    public void AnnounceBarTab(RunTally tally, PartySnapshot party)
    {
        var ranked = party.Members.OrderByDescending(m => tally.TipsyOf(m.Id)).ToList();
        if (ranked.Count == 0 || rules() is not { } ruleSet)
        {
            return;
        }

        var lines = ranked.Select(m =>
        {
            var parts = Enum.GetValues<PenaltyKind>()
                .Where(k => k != PenaltyKind.Water && tally.Count(m.Id, k) > 0)
                .Select(k =>
                {
                    var count = tally.Count(m.Id, k);
                    var label = PenaltyLabels.For(ruleSet, k);
                    return count == 1 ? $"1 {label.Singular}" : $"{count} {label.Plural}";
                });
            return $"{m.DisplayName} (Tipsy {tally.TipsyOf(m.Id)}): {string.Join(", ", parts.DefaultIfEmpty("stone cold sober"))}";
        });

        toasts.Show(new Toast($"BAR TAB — {ranked[0].DisplayName} drank the most", string.Join("\n", lines), ToastOverlay.Gold, Seconds: 12));
    }

    /// <summary>The drinker reacts in a speech bubble ("Here goes nothing!"), styled by how heavy the drink is.</summary>
    private static bool Bubble(IRunState run, PenaltyAssignment assignment, RuleSet ruleSet, PartySnapshot party)
    {
        if (run.GetPlayer(assignment.Player.Value)?.Creature is not { } creature)
        {
            return false;
        }

        var line = Speech.DrinkerLine(assignment.Penalty.Kind);
        if (assignment.Penalty.Count > 1)
        {
            line += $"\n[gold]{PenaltyLabels.Describe(ruleSet, assignment.Penalty)}[/gold]";
        }

        if (assignment.OriginalPlayer is { } from)
        {
            line += $"\n[aqua]thanks, {party.Find(from)?.DisplayName ?? "?"}[/aqua]";
        }
        else if (assignment.OriginalPenalty is not null)
        {
            line += "\n[aqua](capped)[/aqua]";
        }

        return GameVfx.SpeechBubble(creature, line, seconds: 2.8);
    }

    private static string Describe(PenaltyAssignment assignment, RuleSet rules, PartySnapshot party)
    {
        var cause = CauseNames.GetValueOrDefault(assignment.Cause.SourceId);
        var text = (cause, assignment.Cause.Label) switch
        {
            (null, { } label) => label,
            ({ } name, { } label) => $"{name}: {label}",
            ({ } name, null) => name,
            _ => string.Empty,
        };

        if (assignment.OriginalPlayer is { } from)
        {
            text += $" (passed on by {party.Find(from)?.DisplayName ?? "?"})";
        }

        if (assignment.OriginalPenalty is { } original)
        {
            text += $" (capped from {PenaltyLabels.Describe(rules, original)})";
        }

        return text;
    }

    private static Color AccentFor(PenaltyKind kind) => kind switch
    {
        PenaltyKind.Water => new Color("87CEEB"),
        PenaltyKind.Sip => ToastOverlay.Cream,
        PenaltyKind.Finish => ToastOverlay.Gold,
        PenaltyKind.Chug => new Color("FFA518"),
        _ => new Color("FF5555"),
    };
}
