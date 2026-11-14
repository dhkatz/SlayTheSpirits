using Godot;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using SpireKit.Modding;
using SpireKit.Multiplayer;
using SpireKit.UI;
using SpireKit.UI.Controls;
using SpireKit.UI.Settings;

namespace SlayTheSpirits.Presentation;

/// <summary>
/// The "Spirits" tab on the game's Settings screen (labels in title case, like the game's own settings): edits this machine's rules profile, or shows the rules in effect
/// (read-only) during a run or as a client, since the host's locked rules are what everyone plays by.
/// </summary>
internal sealed class RulesSettingsTab
{
    public const string Title = "Spirits";

    private readonly RulesService _rules;
    private readonly ModContext _context;

    private RulesSettingsTab(ModContext context, RulesService rules)
    {
        _context = context;
        _rules = rules;
    }

    public static void Register(ModContext context, RulesService rules)
    {
        var tab = new RulesSettingsTab(context, rules);
        var handle = context.SettingsTabs.Add(Title, tab.Build);
        // Live updates matter when showing someone else's rules (the host's arriving, a run locking them);
        // the player's own edits already show in the controls they used.
        rules.EffectiveChanged += _ =>
        {
            if (!rules.CanEditProfile)
            {
                handle.Refresh();
            }
        };
    }

    private void Build(SettingsPage page)
    {
        var editable = _rules.CanEditProfile;
        var shown = editable ? _rules.Profile : _rules.Effective;

        page.Header("Slay the Spirits");
        if (!editable)
        {
            page.Note(_rules.State.Role == SessionRole.Client
                ? "You're playing by the host's rules. Only the host can change them."
                : "These rules are locked for this run. Changes apply from your next run.");
        }

        if (shown is null)
        {
            page.Note("Waiting for the host's rules…");
            return;
        }

        BuildGeneral(page, shown, editable);
        BuildTriggers(page, shown, editable);
        BuildSharing(page, shown, editable);
        page.Note("More options (drink caps, Tipsy, drink names, per-enemy rules) are in profile.json for now.");
    }

    private void BuildGeneral(SettingsPage page, RuleSet shown, bool editable)
    {
        var names = Presets.All.Select(static p => p.Name).ToList();
        var custom = RuleEditing.PresetIndex(shown) < 0 ? shown : _rules.LastCustom;
        if (custom is not null)
        {
            names.Add(custom.Name);
        }

        var index = RuleEditing.PresetIndex(shown) is var preset and >= 0 ? preset : names.Count - 1;
        page.Choice("Rule Set", names, index, i =>
        {
            Apply(i < Presets.All.Count ? Presets.All[i] : custom!);
            page.Refresh();
        }, editable);

        page.Toggle("Drinking Game On", shown.Enabled, on => Edit(page, r => r with { Enabled = on }), editable);
        page.Toggle("Confirm Each Drink", shown.ConfirmDrinks, on => Edit(page, r => r with { ConfirmDrinks = on }), editable);
        page.Toggle("Tavern Cards", shown.Content.TavernCards, on => Edit(page, r => r with { Content = r.Content with { TavernCards = on } }), editable);
        page.Toggle("Character Cards", shown.Content.CharacterCards, on => Edit(page, r => r with { Content = r.Content with { CharacterCards = on } }), editable);
    }

    private void BuildTriggers(SettingsPage page, RuleSet shown, bool editable)
    {
        page.Header("Who Drinks When");
        foreach (var trigger in RuleEditing.EditableTriggers)
        {
            var rule = RuleEditing.RuleFor(shown, trigger);
            var text = RuleEditing.TextFor(trigger);
            var targets = RuleEditing.TargetsFor(trigger);

            var on = new SettingsToggle { CustomMinimumSize = new Vector2(80, 64) };
            on.SetTicked(rule.Enabled);

            var target = new ChoiceStepper { Compact = true, LabelWidth = 150, Options = [.. targets.Select(RuleEditing.TargetName)] };
            target.SetValueNoSignal(Math.Max(0, IndexOf(targets, rule.Target)));

            var count = new NumberStepper { Compact = true, LabelWidth = 32, Min = 1, Max = Penalty.MaxCount };
            count.SetValueNoSignal(rule.Penalty.Count);

            var kinds = Enum.GetValues<PenaltyKind>();
            var kind = new ChoiceStepper { Compact = true, LabelWidth = 190 };
            kind.Options = KindNames(shown, rule.Penalty.Count);
            kind.SetValueNoSignal((int)rule.Penalty.Kind);

            NumberStepper? threshold = null;
            void Refresh(bool enabled)
            {
                target.Editable = editable && enabled;
                count.Editable = editable && enabled;
                kind.Editable = editable && enabled;
                if (threshold is not null)
                {
                    threshold.Editable = editable && enabled;
                }
            }

            void Save()
            {
                var updated = new TriggerRule(on.IsTicked, new Penalty(kinds[kind.Value], count.Value), targets[target.Value], rule.Threshold);
                rule = updated;
                Edit(page, r => RuleEditing.WithTrigger(r, trigger, updated));
            }

            on.Changed += enabled =>
            {
                Refresh(enabled);
                Save();
            };
            target.ValueChanged += _ => Save();
            count.ValueChanged += c =>
            {
                kind.Options = KindNames(_rules.Profile, c);
                Save();
            };
            kind.ValueChanged += _ => Save();

            page.Row(text.Name, on, target, count, kind);
            on.SetEditable(editable);

            if (trigger.UsesThreshold && text.ThresholdLabel is { } thresholdLabel)
            {
                threshold = page.Number($"    {thresholdLabel}", rule.Threshold ?? 1, 1, text.ThresholdMax, value =>
                {
                    rule = rule with { Threshold = value };
                    Edit(page, r => RuleEditing.WithTrigger(r, trigger, rule));
                }, v => $"{v} {text.ThresholdUnit}", editable);
            }

            Refresh(rule.Enabled);
        }
    }

    private void BuildSharing(SettingsPage page, RuleSet shown, bool editable)
    {
        page.Header("Share");
        var buttons = new List<(string, Action)>
        {
            ("Copy Share Code", () =>
            {
                DisplayServer.ClipboardSet(RuleSetCodec.ToShareCode(shown));
                _context.Toasts.Show(new Toast("Rules Copied", $"Share code for '{shown.Name}' is on your clipboard.", GameStyle.Gold));
            }),
        };

        if (editable)
        {
            buttons.Add(("Paste Share Code", () =>
            {
                if (RuleSetCodec.TryFromShareCode(DisplayServer.ClipboardGet(), out var imported, out var error))
                {
                    Apply(imported);
                    page.Refresh();
                    _context.Toasts.Show(new Toast("Rules Imported", $"Now using '{imported.Name}'.", GameStyle.Gold));
                }
                else
                {
                    _context.Toasts.Show(new Toast("Couldn't Import Rules", error, GameStyle.Cream));
                }
            }));
        }

        page.Buttons([.. buttons]);
    }

    /// <summary>Applies an edit to the profile; rebuilds the page if the rule set's name changed (e.g. to Custom).</summary>
    private void Edit(SettingsPage page, Func<RuleSet, RuleSet> change)
    {
        var before = _rules.Profile;
        var after = RuleEditing.Named(change(before));
        Apply(after);
        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
        {
            page.Refresh();
        }
    }

    private void Apply(RuleSet rules)
    {
        if (_rules.CanEditProfile)
        {
            _rules.UpdateProfile(rules);
        }
    }

    private static List<string> KindNames(RuleSet rules, int count) =>
        [.. Enum.GetValues<PenaltyKind>().Select(k => count == 1 ? PenaltyLabels.For(rules, k).Singular : PenaltyLabels.For(rules, k).Plural)];

    private static int IndexOf(IReadOnlyList<TargetKind> targets, TargetKind target)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i] == target)
            {
                return i;
            }
        }

        return -1;
    }
}
