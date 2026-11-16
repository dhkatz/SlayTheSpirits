using System.Text.Json.Nodes;

namespace SlayTheSpirits.Rules;

/// <summary>
/// Schema v3 → v4: the default Tipsy weights went from 1 / 3 / 5 / 10 to 1 / 6 / 8 / 10 (Sip / Finish / Chug / Shot).
/// Profiles store their weights explicitly, so a profile still on the old defaults is moved to the new ones; weights
/// someone tuned by hand are kept.
/// </summary>
internal static class TipsyWeightsMigration
{
    private const string Key = "tipsy";

    private static readonly Dictionary<string, int> OldDefaults = new(StringComparer.Ordinal)
    {
        ["Water"] = 0, ["Sip"] = 1, ["Finish"] = 3, ["Chug"] = 5, ["Shot"] = 10,
    };

    public static JsonNode MigrateRuleSet(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rules = data.AsObject();
        if (rules[Key] is JsonObject tipsy && IsOldDefaults(tipsy))
        {
            rules.Remove(Key); // Filled with the current defaults on load.
        }

        return rules;
    }

    private static bool IsOldDefaults(JsonObject tipsy) =>
        tipsy.Count == OldDefaults.Count
        && tipsy.All(p => OldDefaults.TryGetValue(p.Key, out var weight)
            && p.Value is JsonValue value && value.TryGetValue<int>(out var actual) && actual == weight);
}
