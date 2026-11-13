namespace SlayTheSpirits.Penalties;

/// <summary>What a player has to drink, from lightest to heaviest.</summary>
/// <remarks>Numeric order is severity order; <see cref="Water"/> is the non-alcoholic exception.</remarks>
public enum PenaltyKind
{
    /// <summary>A hydration break. Never counted toward caps.</summary>
    Water = 0,
    Sip = 1,
    Drink = 2,
    Shot = 3,
    Chug = 4,

    /// <summary>Finish whatever is in your glass.</summary>
    Finish = 5,
}

/// <summary>A quantity of a <see cref="PenaltyKind"/>, e.g. "2 Shots".</summary>
public readonly record struct Penalty
{
    public const int MaxCount = 10;

    [System.Text.Json.Serialization.JsonConstructor]
    public Penalty(PenaltyKind kind, int count = 1)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown penalty kind.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxCount);
        Kind = kind;
        Count = count;
    }

    public PenaltyKind Kind { get; }

    public int Count { get; }

    public bool IsAlcoholic => Kind != PenaltyKind.Water;

    /// <summary>The next lighter alcoholic penalty with the same count, or null if already a Sip/Water.</summary>
    public Penalty? Downgraded() => Kind switch
    {
        PenaltyKind.Water or PenaltyKind.Sip => null,
        _ => new Penalty(Kind - 1, Count),
    };

    public override string ToString() => Count == 1 ? Kind.ToString() : $"{Count}x {Kind}";

    public static Penalty Of(PenaltyKind kind, int count = 1) => new(kind, count);
}
