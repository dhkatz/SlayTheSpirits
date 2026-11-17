using System.Text.Json.Nodes;

namespace SlayTheSpirits.Engine;

/// <summary>
/// Run state v2 → v3: the rest site's Chug and Shot became the two tabs (<see cref="RestSiteTabs"/>). A Chug taken this
/// act counts as the tab for cards, a Shot as the tab for a relic.
/// </summary>
internal static class RestSiteTabMigration
{
    private const string OldKey = "restSiteDrinksThisAct";
    private const string NewKey = "restSiteTabsThisAct";

    private static readonly Dictionary<string, string> Tabs = new(StringComparer.Ordinal)
    {
        ["Chug"] = nameof(RestSiteTab.Open),
        ["Shot"] = nameof(RestSiteTab.RunUp),
    };

    public static JsonNode MigrateRunState(JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var state = data.AsObject();
        if (state[OldKey] is not JsonObject byPlayer)
        {
            return state;
        }

        var migrated = new JsonObject();
        foreach (var (player, counts) in byPlayer)
        {
            var tabs = new JsonObject();
            if (counts is JsonObject byKind)
            {
                foreach (var (kind, count) in byKind)
                {
                    if (Tabs.TryGetValue(kind, out var tab))
                    {
                        tabs[tab] = count?.DeepClone();
                    }
                }
            }

            migrated[player] = tabs;
        }

        state.Remove(OldKey);
        state[NewKey] = migrated;
        return state;
    }
}
