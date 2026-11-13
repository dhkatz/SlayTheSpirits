namespace SlayTheSpirits.Party;

/// <summary>Stable identity of a player in a session (the game's network id).</summary>
public readonly record struct PlayerId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>A player at the table. Dead characters still drink.</summary>
public sealed record PartyMember(PlayerId Id, string DisplayName, bool IsAlive = true);

/// <summary>
/// Read-only view of the party. <see cref="Members"/> must be in the same order on every peer
/// (e.g. player slot order) because random targeting indexes into it.
/// </summary>
public interface IPartyView
{
    IReadOnlyList<PartyMember> Members { get; }
}

/// <summary>Immutable <see cref="IPartyView"/> snapshot.</summary>
public sealed record PartySnapshot(IReadOnlyList<PartyMember> Members) : IPartyView
{
    public static PartySnapshot Of(params PartyMember[] members) => new(members);

    public PartyMember? Find(PlayerId id) => Members.FirstOrDefault(m => m.Id == id);
}
