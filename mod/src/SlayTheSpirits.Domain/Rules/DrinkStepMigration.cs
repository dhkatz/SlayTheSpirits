using System.Text.Json;
using System.Text.Json.Nodes;
using SlayTheSpirits.Penalties;

namespace SlayTheSpirits.Rules;

/// <summary>
/// Schema v1 → v2: the ladder lost its "Drink" step (between Sip and Shot) and was reordered to
/// Water &lt; Sip &lt; Finish &lt; Chug &lt; Shot. Kinds are stored by name, so only "Drink" needs rewriting.
/// </summary>
internal static class DrinkStepMigration
{
    private const string Removed = "Drink";
    private static readonly string Replacement = nameof(PenaltyKind.Sip);

    // v1 default Tipsy weights; profiles saved them explicitly, which would otherwise pin the old values.
    private static readonly Dictionary<string, int> OldDefaultTipsy = new(StringComparer.Ordinal)
    {
        ["Water"] = 0, ["Sip"] = 1, ["Drink"] = 2, ["Shot"] = 3, ["Chug"] = 4, ["Finish"] = 5,
    };

    /// <summary>
    /// A v1 rule set. Built-in presets become their v2 versions (the old ladder can't be mapped faithfully);
    /// anything else keeps its rules with Drink penalties turned into Sips.
    /// </summary>
    public static JsonNode MigrateRuleSet(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rules = data.AsObject();
        var name = rules["name"]?.GetValue<string>();
        if (Presets.All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal)) is { } preset)
        {
            return JsonSerializer.SerializeToNode(preset, DomainJsonContext.Default.RuleSet)!;
        }

        RenameKinds(rules["triggers"]);
        RenameKinds(rules["enemies"]);
        DropKey(rules["capsPerAct"]);
        DropKey(rules["labels"]);
        if (rules["tipsy"] is JsonObject tipsy && IsOldDefaultTipsy(tipsy))
        {
            rules.Remove("tipsy"); // Filled with the current defaults on load.
        }
        else
        {
            DropKey(rules["tipsy"]);
        }

        return rules;
    }

    /// <summary>A v1 run state: Drink penalties become Sips and their counts are added to the Sip counts.</summary>
    public static JsonNode MigrateRunState(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var state = data.AsObject();
        foreach (var counts in PerPlayer(state["actCounts"]).Concat(PerPlayer(state["tally"]?["totals"])))
        {
            MergeCounts(counts);
        }

        RenameKinds(state["tally"]?["worstMoments"]);
        return state;
    }

    /// <summary>Every <c>"kind": "Drink"</c> anywhere below <paramref name="node"/> becomes Sip.</summary>
    private static void RenameKinds(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["kind"] is JsonValue kind && kind.TryGetValue<string>(out var value) && value == Removed)
                {
                    obj["kind"] = Replacement;
                }

                foreach (var (_, child) in obj.ToList())
                {
                    RenameKinds(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    RenameKinds(child);
                }

                break;
        }
    }

    private static void DropKey(JsonNode? keyedByKind) => (keyedByKind as JsonObject)?.Remove(Removed);

    private static void MergeCounts(JsonNode? keyedByKind)
    {
        if (keyedByKind is not JsonObject counts || counts[Removed] is not JsonValue drink)
        {
            return;
        }

        var sips = counts[Replacement]?.GetValue<int>() ?? 0;
        counts[Replacement] = sips + drink.GetValue<int>();
        counts.Remove(Removed);
    }

    private static IEnumerable<JsonNode?> PerPlayer(JsonNode? byPlayer) =>
        byPlayer is JsonObject obj ? obj.Select(static p => p.Value) : [];

    private static bool IsOldDefaultTipsy(JsonObject tipsy) =>
        tipsy.Count == OldDefaultTipsy.Count
        && tipsy.All(p => OldDefaultTipsy.TryGetValue(p.Key, out var weight) && p.Value?.GetValue<int>() == weight);
}
