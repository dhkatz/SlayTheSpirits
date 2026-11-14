using System.Text.Json;
using System.Text.Json.Nodes;
using SlayTheSpirits.Engine;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;

if (args.Length != 1)
{
    await Console.Error.WriteLineAsync("Usage: SlayTheSpirits.DocsExport <output.json>").ConfigureAwait(false);
    return 1;
}

var kinds = Enum.GetValues<PenaltyKind>();

var ladder = new JsonArray([.. kinds.Select(kind => (JsonNode)new JsonObject
{
    ["kind"] = kind.ToString(),
    ["singular"] = PenaltyLabels.Defaults[kind].Singular,
    ["plural"] = PenaltyLabels.Defaults[kind].Plural,
    ["description"] = Glossary.For(Presets.Standard, kind).Body,
    ["tipsy"] = TipsyWeights.Defaults[kind],
})]);

var presets = new JsonArray([.. Presets.All.Select(rules => (JsonNode)Preset(rules))]);

var data = new JsonObject
{
    ["ladder"] = ladder,
    ["presets"] = presets,
    ["restSiteDrinkKinds"] = new JsonArray([.. RestSiteDrinks.Kinds.Select(k => (JsonNode)k.ToString())]),
    ["tipsyTitle"] = Glossary.Tipsy(Presets.Standard).Title,
    ["tipsyDescription"] = Glossary.Tipsy(Presets.Standard).Body,
};

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, data.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
Console.WriteLine($"Wrote {output}");
return 0;

static JsonObject Preset(RuleSet rules) => new()
{
    ["name"] = rules.Name,
    ["enabled"] = rules.Enabled,
    ["restSiteDrinks"] = rules.RestSiteDrinks.ToString(),
    ["confirmDrinks"] = rules.ConfirmDrinks,
    ["caps"] = new JsonObject(rules.CapsPerAct.OrderBy(static c => c.Key).Select(static c => KeyValuePair.Create(c.Key.ToString(), (JsonNode?)c.Value))),
    ["labels"] = new JsonObject(Enum.GetValues<PenaltyKind>().Select(kind => KeyValuePair.Create(kind.ToString(), (JsonNode?)new JsonObject
    {
        ["singular"] = PenaltyLabels.For(rules, kind).Singular,
        ["plural"] = PenaltyLabels.For(rules, kind).Plural,
    }))),
    ["triggers"] = new JsonArray([.. RuleEditing.EditableTriggers.Select(trigger => (JsonNode)Trigger(rules, trigger))]),
};

static JsonObject Trigger(RuleSet rules, TriggerDefinition trigger)
{
    var rule = RuleEditing.RuleFor(rules, trigger);
    var text = RuleEditing.TextFor(trigger);
    return new JsonObject
    {
        ["id"] = trigger.Id.Value,
        ["name"] = text.Name,
        ["enabled"] = rule.Enabled,
        ["kind"] = rule.Penalty.Kind.ToString(),
        ["count"] = rule.Penalty.Count,
        ["drink"] = PenaltyLabels.Describe(rules, rule.Penalty),
        ["target"] = RuleEditing.TargetName(rule.Target),
        ["threshold"] = trigger.UsesThreshold ? rule.Threshold : null,
        ["thresholdLabel"] = text.ThresholdLabel,
        ["thresholdUnit"] = text.ThresholdUnit,
    };
}
