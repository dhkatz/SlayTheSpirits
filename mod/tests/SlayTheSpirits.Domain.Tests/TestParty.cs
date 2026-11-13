using SlayTheSpirits.Party;

namespace SlayTheSpirits.Tests;

internal static class TestParty
{
    public static readonly PlayerId Alice = new(101);
    public static readonly PlayerId Bob = new(202);
    public static readonly PlayerId Cara = new(303);
    public static readonly PlayerId Dave = new(404);

    public static PartySnapshot Four { get; } = PartySnapshot.Of(
        new(Alice, "Alice"), new(Bob, "Bob"), new(Cara, "Cara"), new(Dave, "Dave"));

    public static PartySnapshot Solo { get; } = PartySnapshot.Of(new PartyMember(Alice, "Alice"));
}
