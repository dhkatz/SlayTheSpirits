using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;

namespace SlayTheSpirits.Engine;

/// <summary>Maps a <see cref="TriggerOccurrence"/> to a <see cref="PenaltyRequest"/> using a <see cref="RuleSet"/>.</summary>
public static class RuleResolver
{
    /// <summary>Returns the request the rules call for, or null if nothing should happen.</summary>
    public static PenaltyRequest? Resolve(RuleSet rules, TriggerOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(occurrence);
        if (!rules.Enabled)
        {
            return null;
        }

        var definition = BuiltInTriggers.Find(occurrence.Trigger.Value);
        var rule = definition?.IsKeyedByDetail == true
            ? occurrence.Detail is { } detail ? rules.Enemies.GetValueOrDefault(detail) : null
            : rules.Triggers.GetValueOrDefault(occurrence.Trigger.Value);

        if (rule is not { Enabled: true })
        {
            return null;
        }

        if (rule.Threshold is { } threshold && (occurrence.Magnitude ?? 0) < threshold)
        {
            return null;
        }

        return new PenaltyRequest(
            rule.Penalty,
            rule.Target,
            occurrence.Subject,
            Instigator: null,
            new PenaltyCause(occurrence.Trigger.Value, occurrence.Label));
    }
}
