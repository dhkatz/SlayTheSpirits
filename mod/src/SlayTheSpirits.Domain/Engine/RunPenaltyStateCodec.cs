using SlayTheSpirits.Rules;
using SpireKit.Storage;

namespace SlayTheSpirits.Engine;

/// <summary>Serialization of <see cref="RunPenaltyState"/>, saved with the run and sent to clients on load.</summary>
public static class RunPenaltyStateCodec
{
    /// <summary>Bump when the state's JSON shape changes, and register a migration in <see cref="Format"/>.</summary>
    public const int SchemaVersion = 3;

    public static VersionedJsonDocument<RunPenaltyState> Format { get; } = new(
        SchemaVersion,
        DomainJsonContext.Default.RunPenaltyState,
        migrations:
        [
            new JsonMigration(1, DrinkStepMigration.MigrateRunState),
            new JsonMigration(2, RestSiteTabMigration.MigrateRunState),
        ],
        defaults: static () => new RunPenaltyState());

    public static string ToJson(RunPenaltyState state) => Format.Encode(state);

    /// <summary>The decoded state, or null if the payload is unreadable (the run then continues from zero).</summary>
    public static RunPenaltyState? FromJson(string json)
    {
        var result = Format.Decode(json, static () => new RunPenaltyState());
        return result.Status == DecodeStatus.Invalid ? null : result.Value;
    }
}
