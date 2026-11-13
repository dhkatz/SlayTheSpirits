using SlayTheSpirits.Triggers;

namespace SlayTheSpirits.Rules;

/// <summary>Checks a <see cref="RuleSet"/> for values the engine cannot honor.</summary>
/// <remarks>
/// Unknown trigger ids are allowed (a newer mod version may add triggers); they're simply never raised.
/// </remarks>
public static class RuleSetValidator
{
    public const int MaxNameLength = 48;

    public static IReadOnlyList<string> Validate(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(rules.Name) || rules.Name.Length > MaxNameLength)
        {
            problems.Add($"Name must be 1-{MaxNameLength} characters.");
        }

        foreach (var (id, rule) in rules.Triggers)
        {
            if (BuiltInTriggers.Find(id) is not { } definition)
            {
                continue;
            }

            if (definition.IsKeyedByDetail)
            {
                problems.Add($"Trigger '{id}' is configured per-detail (see Enemies) and cannot have a single rule.");
            }

            CheckRule(problems, id, rule, definition.HasSubject, definition.UsesThreshold);
        }

        foreach (var (monsterId, rule) in rules.Enemies)
        {
            CheckRule(problems, $"enemy '{monsterId}'", rule, hasSubject: false, usesThreshold: false);
        }

        foreach (var (kind, cap) in rules.CapsPerAct)
        {
            if (cap < 0)
            {
                problems.Add($"Cap for {kind} must not be negative.");
            }
        }

        foreach (var (kind, weight) in rules.Tipsy)
        {
            if (weight is < 0 or > TipsyWeights.MaxPerUnit)
            {
                problems.Add($"Tipsy for {kind} must be 0-{TipsyWeights.MaxPerUnit}.");
            }
        }

        foreach (var (kind, label) in rules.Labels)
        {
            if (string.IsNullOrWhiteSpace(label.Singular) || string.IsNullOrWhiteSpace(label.Plural))
            {
                problems.Add($"Label for {kind} must not be empty.");
            }
        }

        return problems;
    }

    private static void CheckRule(List<string> problems, string name, TriggerRule rule, bool hasSubject, bool usesThreshold)
    {
        if (!hasSubject && rule.Target is TargetKind.Subject or TargetKind.Others or TargetKind.RandomOther)
        {
            problems.Add($"Rule '{name}' targets {rule.Target}, but that trigger has no subject player.");
        }

        if (!usesThreshold && rule.Threshold is not null)
        {
            problems.Add($"Rule '{name}' sets a threshold, but that trigger has no magnitude.");
        }

        if (rule.Threshold is < 0)
        {
            problems.Add($"Rule '{name}' threshold must not be negative.");
        }
    }
}
