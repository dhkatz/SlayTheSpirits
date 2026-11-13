using System.Text;

namespace SpireKit.Determinism;

/// <summary>
/// Process-independent hashing. Unlike <see cref="string.GetHashCode()"/> (randomized per process),
/// these values are identical on every peer and across runs, so they are safe for lockstep logic,
/// wire IDs and RNG stream derivation.
/// </summary>
public static class StableHash
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>64-bit FNV-1a over the UTF-8 bytes of <paramref name="value"/>.</summary>
    public static ulong Fnv1a64(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Fnv1a64(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>64-bit FNV-1a over <paramref name="bytes"/>.</summary>
    public static ulong Fnv1a64(ReadOnlySpan<byte> bytes)
    {
        var hash = FnvOffset;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>Mixes two 64-bit values into one (SplitMix64 finalizer over a combination).</summary>
    public static ulong Combine(ulong a, ulong b) => SplitMix64.Mix(a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2)));
}
