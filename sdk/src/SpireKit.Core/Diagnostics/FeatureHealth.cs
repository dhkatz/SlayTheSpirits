namespace SpireKit.Diagnostics;

/// <summary>Whether a mod feature is working in the current game build.</summary>
public enum FeatureStatus
{
    Healthy,

    /// <summary>Works with reduced functionality (e.g. optional data unavailable).</summary>
    Degraded,

    /// <summary>Switched off because something it depends on is missing or threw.</summary>
    Disabled,
}

/// <summary>One line of a health report.</summary>
public sealed record FeatureReport(string Owner, string Feature, FeatureStatus Status, string? Detail = null);

/// <summary>
/// Collects per-feature health so a game update degrades a mod gracefully (one trigger stops working)
/// instead of crashing it, and so the log shows exactly what broke right after a patch.
/// </summary>
public sealed class FeatureHealthRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Owner, string Feature), FeatureReport> _reports = [];

    public void Report(FeatureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (_gate)
        {
            _reports[(report.Owner, report.Feature)] = report;
        }
    }

    public void Report(string owner, string feature, FeatureStatus status, string? detail = null) =>
        Report(new FeatureReport(owner, feature, status, detail));

    public bool IsEnabled(string owner, string feature)
    {
        lock (_gate)
        {
            return !_reports.TryGetValue((owner, feature), out var r) || r.Status != FeatureStatus.Disabled;
        }
    }

    public IReadOnlyList<FeatureReport> Snapshot(string? owner = null)
    {
        lock (_gate)
        {
            return [.. _reports.Values
                .Where(r => owner is null || r.Owner == owner)
                .OrderBy(static r => r.Owner, StringComparer.Ordinal)
                .ThenBy(static r => r.Feature, StringComparer.Ordinal)];
        }
    }

    /// <summary>Human-readable summary, e.g. <c>SlayTheSpirits: 11/12 healthy; DISABLED trigger.bigHit (method not found)</c>.</summary>
    public string Summarize(string owner)
    {
        var reports = Snapshot(owner);
        var healthy = reports.Count(static r => r.Status == FeatureStatus.Healthy);
        var problems = reports
            .Where(static r => r.Status != FeatureStatus.Healthy)
            .Select(static r => $"{r.Status.ToString().ToUpperInvariant()} {r.Feature}{(r.Detail is null ? string.Empty : $" ({r.Detail})")}");
        var head = $"{owner}: {healthy}/{reports.Count} healthy";
        return problems.Any() ? head + "; " + string.Join("; ", problems) : head;
    }
}
