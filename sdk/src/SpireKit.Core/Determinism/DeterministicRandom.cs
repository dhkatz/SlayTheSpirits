namespace SpireKit.Determinism;

/// <summary>
/// A small, fast, fully specified PRNG (xoshiro256**) whose output is identical on every platform and peer.
/// </summary>
/// <remarks>
/// Mods must never draw from the game's own RNG streams: doing so shifts the game's sequence and
/// changes card rewards, map generation, etc. Instead derive an independent stream from the run seed
/// with <see cref="ForStream"/>. As long as every peer draws in the same order (which holds for
/// effects driven by the lockstep action queue) results agree everywhere.
/// </remarks>
public sealed class DeterministicRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>Creates a generator from a 64-bit seed (expanded via SplitMix64).</summary>
    public DeterministicRandom(ulong seed)
    {
        var sm = new SplitMix64(seed);
        _s0 = sm.Next();
        _s1 = sm.Next();
        _s2 = sm.Next();
        _s3 = sm.Next();
    }

    private DeterministicRandom(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        (_s0, _s1, _s2, _s3) = (s0, s1, s2, s3);
    }

    /// <summary>Number of values drawn so far. Useful for persisting/restoring position.</summary>
    public long Draws { get; private set; }

    /// <summary>
    /// Derives an independent generator for a named stream, e.g.
    /// <c>ForStream(runSeed, "SlayTheSpirits/targets")</c>.
    /// </summary>
    public static DeterministicRandom ForStream(ulong rootSeed, string streamName) =>
        new(StableHash.Combine(rootSeed, StableHash.Fnv1a64(streamName)));

    /// <summary>Creates a generator from raw xoshiro state. For verifying against reference vectors.</summary>
    internal static DeterministicRandom FromRawState(ulong s0, ulong s1, ulong s2, ulong s3) => new(s0, s1, s2, s3);

    /// <summary>Creates a generator and fast-forwards it by <paramref name="draws"/> values.</summary>
    public static DeterministicRandom Restore(ulong seed, long draws)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(draws);
        var rng = new DeterministicRandom(seed);
        for (long i = 0; i < draws; i++)
        {
            rng.NextUInt64();
        }

        return rng;
    }

    /// <summary>Next raw 64-bit value.</summary>
    public ulong NextUInt64()
    {
        Draws++;
        var result = ulong.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = ulong.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, <paramref name="exclusiveMax"/>) without modulo bias.</summary>
    public int NextInt(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        var bound = (ulong)exclusiveMax;
        var threshold = (ulong.MaxValue - bound + 1) % bound;
        while (true)
        {
            var r = NextUInt64();
            if (r >= threshold)
            {
                return (int)(r % bound);
            }
        }
    }

    /// <summary>Picks one element uniformly.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        return items[NextInt(items.Count)];
    }
}

/// <summary>SplitMix64, used for seeding and hashing.</summary>
internal struct SplitMix64(ulong seed)
{
    private ulong _state = seed;

    public ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        return Mix(_state);
    }

    public static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
