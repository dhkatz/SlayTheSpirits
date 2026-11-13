using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SpireKit.Sharing;
using SpireKit.Storage;

namespace SpireKit.Tests;

public sealed record Settings(string Name, int Volume);

[JsonSerializable(typeof(Settings))]
internal sealed partial class TestJsonContext : JsonSerializerContext;

public sealed class StorageTests
{
    private static readonly IJsonMigration RenameLoudness = new JsonMigration(1, data =>
    {
        var obj = data.AsObject();
        obj["Volume"] = obj["Loudness"]!.DeepClone();
        obj.Remove("Loudness");
        return obj;
    });

    private static VersionedJsonDocument<Settings> Format(Func<Settings, IReadOnlyList<string>>? validate = null) =>
        new(2, TestJsonContext.Default.Settings, [RenameLoudness], validate);

    [Fact]
    public void Round_trips_at_current_version()
    {
        var format = Format();
        var result = format.Decode(format.Encode(new Settings("a", 3)), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Ok, result.Status);
        Assert.Equal(new Settings("a", 3), result.Value);
    }

    [Fact]
    public void Migrates_old_payloads()
    {
        var v1 = new JsonObject { ["schema"] = 1, ["data"] = new JsonObject { ["Name"] = "old", ["Loudness"] = 9 } };

        var result = Format().Decode(v1.ToJsonString(), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Migrated, result.Status);
        Assert.Equal(new Settings("old", 9), result.Value);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schema\": 99, \"data\": {}}")]
    [InlineData("{\"data\": {}}")]
    public void Falls_back_on_unreadable_input(string json)
    {
        var result = Format().Decode(json, () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Invalid, result.Status);
        Assert.Equal("default", result.Value.Name);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Falls_back_when_validation_fails()
    {
        var format = Format(s => s.Volume > 10 ? ["too loud"] : []);

        var result = format.Decode(format.Encode(new Settings("x", 11)), () => new Settings("default", 0));

        Assert.Equal(DecodeStatus.Invalid, result.Status);
        Assert.Contains("too loud", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_file_creates_defaults_and_preserves_corrupt_files()
    {
        var store = new InMemoryFileStore();
        var file = new JsonDocumentFile<Settings>(store, "cfg/settings.json", Format(), () => new Settings("default", 1));

        Assert.Equal("default", file.Load().Name);
        Assert.NotNull(store.ReadText("cfg/settings.json"));

        store.WriteText("cfg/settings.json", "{ broken");
        Assert.Equal("default", file.Load().Name);
        Assert.Equal("{ broken", store.ReadText("cfg/settings.json.corrupt-1"));
    }

    [Fact]
    public void File_store_rejects_escaping_paths()
    {
        var store = new InMemoryFileStore();

        Assert.Throws<ArgumentException>(() => store.WriteText("../evil.json", "x"));
        Assert.Throws<ArgumentException>(() => store.WriteText("C:/evil.json", "x"));
    }

    [Fact]
    public void Share_codes_round_trip_and_reject_foreign_input()
    {
        var code = ShareCode.Encode("TEST1", "{\"hello\":\"world\"}");

        Assert.StartsWith("TEST1:", code, StringComparison.Ordinal);
        Assert.True(ShareCode.TryDecode("TEST1", $"  {code}\n", out var payload));
        Assert.Equal("{\"hello\":\"world\"}", payload);
        Assert.False(ShareCode.TryDecode("OTHER1", code, out _));
        Assert.False(ShareCode.TryDecode("TEST1", "TEST1:!!!", out _));
        Assert.False(ShareCode.TryDecode("TEST1", null, out _));
    }
}
