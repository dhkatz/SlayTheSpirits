using System.Text.Json.Nodes;

namespace SlayTheSpirits.Rules;

/// <summary>
/// Schema v2 → v3: <see cref="RuleSet.RestSiteTabs"/> (then rest site drinks) went from on/off to a
/// <see cref="RestSiteTabLimit"/>. On becomes <see cref="RestSiteTabLimit.OncePerAct"/> (what on meant), off stays off.
/// </summary>
internal static class RestSiteLimitMigration
{
    private const string Key = "restSiteDrinks";

    public static JsonNode MigrateRuleSet(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var rules = data.AsObject();
        if (rules[Key] is JsonValue value && value.TryGetValue<bool>(out var on))
        {
            rules[Key] = on ? nameof(RestSiteTabLimit.OncePerAct) : nameof(RestSiteTabLimit.Off);
        }

        return rules;
    }
}
