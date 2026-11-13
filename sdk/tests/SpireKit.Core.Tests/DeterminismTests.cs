using SpireKit.Determinism;

namespace SpireKit.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void Fnv1a64_matches_reference_vectors()
    {
        // Reference values for 64-bit FNV-1a. If these change, every derived seed and wire id changes.
        Assert.Equal(0xcbf29ce484222325UL, StableHash.Fnv1a64(string.Empty));
        Assert.Equal(0xaf63dc4c8601ec8cUL, StableHash.Fnv1a64("a"));
        Assert.Equal(0x85944171f73967e8UL, StableHash.Fnv1a64("foobar"));
    }

    [Fact]
    public void Xoshiro256StarStar_matches_reference_vectors()
    {
        // Reference output of xoshiro256** (Blackman & Vigna) for state {1, 2, 3, 4}.
        var rng = DeterministicRandom.FromRawState(1, 2, 3, 4);

        Assert.Equal(11520UL, rng.NextUInt64());
        Assert.Equal(0UL, rng.NextUInt64());
        Assert.Equal(1509978240UL, rng.NextUInt64());
        Assert.Equal(1215971899390074240UL, rng.NextUInt64());
    }

    [Fact]
    public void Same_seed_and_stream_produce_identical_sequences()
    {
        var a = DeterministicRandom.ForStream(42, "mod/stream");
        var b = DeterministicRandom.ForStream(42, "mod/stream");

        Assert.Equal(Draw(a, 100), Draw(b, 100));
    }

    [Fact]
    public void Different_streams_diverge()
    {
        var a = DeterministicRandom.ForStream(42, "mod/one");
        var b = DeterministicRandom.ForStream(42, "mod/two");

        Assert.NotEqual(Draw(a, 10), Draw(b, 10));
    }

    [Fact]
    public void Restore_resumes_exactly_where_the_original_left_off()
    {
        var original = new DeterministicRandom(7);
        Draw(original, 13);
        var restored = DeterministicRandom.Restore(7, original.Draws);

        Assert.Equal(Draw(original, 20), Draw(restored, 20));
    }

    [Fact]
    public void NextInt_stays_in_range_and_covers_it()
    {
        var rng = new DeterministicRandom(1);
        var seen = new HashSet<int>();
        for (var i = 0; i < 1000; i++)
        {
            var v = rng.NextInt(4);
            Assert.InRange(v, 0, 3);
            seen.Add(v);
        }

        Assert.Equal(4, seen.Count);
    }

    [Fact]
    public void Sequence_is_pinned()
    {
        // Guards against accidental algorithm changes, which would desync peers on different mod builds.
        var rng = new DeterministicRandom(12345);
        Assert.Equal([3, 8, 6, 5, 4],Enumerable.Range(0, 5).Select(_ => rng.NextInt(10)).ToArray());
    }

    private static ulong[] Draw(DeterministicRandom rng, int n) => [.. Enumerable.Range(0, n).Select(_ => rng.NextUInt64())];
}
