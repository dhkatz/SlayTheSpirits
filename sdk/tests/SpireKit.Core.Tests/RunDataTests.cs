using SpireKit.Logging;
using SpireKit.Runs;

namespace SpireKit.Tests;

public sealed class RunDataTests
{
    private static readonly RunIdentity RunA = new("ABC123", 1_700_000_000);
    private static readonly RunIdentity RunB = new("ABC123", 1_700_000_999);

    [Fact]
    public void Bundle_round_trips()
    {
        var bundle = new RunDataBundle(RunA, new Dictionary<string, string> { ["penalties"] = "{\"x\":1}", ["other"] = "text" });

        var decoded = RunDataBundle.TryDecode(bundle.Encode());

        Assert.NotNull(decoded);
        Assert.Equal(RunA, decoded.Run);
        Assert.Equal(bundle.Slots, decoded.Slots);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"format\":2,\"seed\":\"A\",\"startTime\":1,\"slots\":{}}")]
    [InlineData("{\"format\":1,\"startTime\":1,\"slots\":{}}")]
    public void Unreadable_bundles_decode_to_null(string? text) => Assert.Null(RunDataBundle.TryDecode(text));

    [Fact]
    public void Restore_hands_each_slot_its_captured_payload()
    {
        var registry = new RunDataRegistry();
        var live = "state-1";
        string? restored = "untouched";
        registry.Register("penalties", () => live, s => restored = s);

        registry.Capture(RunA);
        live = "state-2";
        registry.Restore(RunA);

        Assert.Equal("state-1", restored);
    }

    [Fact]
    public void Restore_without_data_gives_null_so_slots_start_fresh()
    {
        var registry = new RunDataRegistry();
        string? restored = "stale";
        registry.Register("penalties", () => "state", s => restored = s);

        registry.Capture(RunA);
        registry.Restore(RunB); // Same seed, different run.

        Assert.Null(restored);
    }

    [Fact]
    public void Remembered_bundles_are_found_by_run()
    {
        var registry = new RunDataRegistry();
        var bundle = new RunDataBundle(RunB, new Dictionary<string, string> { ["penalties"] = "from host" });

        registry.Remember(bundle);

        Assert.Same(bundle, registry.Find(RunB));
        Assert.Null(registry.Find(RunA));
    }

    [Fact]
    public void A_failing_slot_does_not_stop_the_others()
    {
        var log = new RecordingLog();
        var registry = new RunDataRegistry(log);
        string? restored = null;
        registry.Register("broken", () => throw new InvalidOperationException("boom"), _ => throw new InvalidOperationException("boom"));
        registry.Register("fine", () => "ok", s => restored = s);

        var bundle = registry.Capture(RunA);
        registry.Restore(RunA);

        Assert.Equal(["fine"], bundle.Slots.Keys);
        Assert.Equal("ok", restored);
        Assert.Equal(2, log.Entries.Count(e => e.Level == LogLevel.Error));
    }

    [Fact]
    public void Slot_names_must_be_unique()
    {
        var registry = new RunDataRegistry();
        registry.Register("penalties", () => null, _ => { });

        Assert.Throws<InvalidOperationException>(() => registry.Register("penalties", () => null, _ => { }));
    }
}
