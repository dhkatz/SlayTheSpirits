namespace SlayTheSpirits.Penalties;

/// <summary>What a player has to drink, from lightest to heaviest.</summary>
/// <remarks>
/// Numeric order is severity order (caps downgrade one step at a time); <see cref="Water"/> is the non-alcoholic
/// exception. Everything up to <see cref="Chug"/> is measured against the drink in your hand; a <see cref="Shot"/>
/// is liquor, a full standard drink on its own, so it is the rare top step. Saved rules and runs store kinds by
/// name, and "Drink" (an old step between Sip and Shot) is migrated to <see cref="Sip"/>.
/// </remarks>
public enum PenaltyKind
{
    /// <summary>A hydration break. Never counted toward caps.</summary>
    Water = 0,

    /// <summary>A sip of whatever you're drinking.</summary>
    Sip = 1,

    /// <summary>Finish whatever is left in your drink.</summary>
    Finish = 2,

    /// <summary>Chug a whole drink: a beer, a seltzer, any can or bottle.</summary>
    Chug = 3,

    /// <summary>A shot of liquor. Reserved for disasters and big payoffs.</summary>
    Shot = 4,
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
