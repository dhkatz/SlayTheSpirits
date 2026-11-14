using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using SlayTheSpirits.Engine;
using SpireKit.Sharing;
using SpireKit.Storage;

namespace SlayTheSpirits.Rules;

/// <summary>Serialization of <see cref="RuleSet"/> for disk, network and share codes.</summary>
public static class RuleSetCodec
{
    /// <summary>
    /// Bump when the RuleSet JSON shape changes incompatibly (renames, meaning changes), and register a migration in
    /// <see cref="Format"/>. Adding a setting needs neither: files without it get the <see cref="RuleSet"/> default.
    /// </summary>
    public const int SchemaVersion = 3;

    /// <summary>Prefix for share codes. Bump the digit only if old codes can no longer be read.</summary>
    public const string ShareCodePrefix = "SPIRITS1";

    public static VersionedJsonDocument<RuleSet> Format { get; } = new(
        SchemaVersion,
        DomainJsonContext.Default.RuleSet,
        migrations:
        [
            new JsonMigration(1, DrinkStepMigration.MigrateRuleSet),
            new JsonMigration(2, RestSiteLimitMigration.MigrateRuleSet),
        ],
        validate: RuleSetValidator.Validate,
        defaults: static () => new RuleSet());

    public static string ToJson(RuleSet rules) => Format.Encode(rules);

    public static DecodeResult<RuleSet> FromJson(string json) => Format.Decode(json, static () => Presets.Standard);

    public static string ToShareCode(RuleSet rules) => ShareCode.Encode(ShareCodePrefix, ToJson(rules));

    public static bool TryFromShareCode(string? code, [NotNullWhen(true)] out RuleSet? rules, out string? error)
    {
        rules = null;
        if (!ShareCode.TryDecode(ShareCodePrefix, code, out var json))
        {
            error = "Not a valid Slay the Spirits rules code.";
            return false;
        }

        var result = FromJson(json);
        if (result.Status == DecodeStatus.Invalid)
        {
            error = result.Error;
            return false;
        }

        rules = result.Value;
        error = null;
        return true;
    }
}

/// <summary>Source-generated System.Text.Json metadata for domain types (no runtime reflection).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(RuleSet))]
[JsonSerializable(typeof(RunPenaltyState))]
internal sealed partial class DomainJsonContext : JsonSerializerContext;
