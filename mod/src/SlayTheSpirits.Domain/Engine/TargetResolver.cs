using SlayTheSpirits.Party;
using SlayTheSpirits.Rules;
using SpireKit.Determinism;

namespace SlayTheSpirits.Engine;

/// <summary>Turns a request's <see cref="TargetKind"/> into concrete players.</summary>
public static class TargetResolver
{
    public static IReadOnlyList<PlayerId> Resolve(PenaltyRequest request, IPartyView party, DeterministicRandom rng)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        var members = party.Members;
        return request.Target switch
        {
            TargetKind.Subject => request.Subject is { } subject ? [subject] : [],
            TargetKind.Everyone => [.. members.Select(static m => m.Id)],
            TargetKind.Others => request.Subject is { } subject
                ? [.. members.Where(m => m.Id != subject).Select(static m => m.Id)]
                : [],
            TargetKind.RandomPlayer => members.Count > 0 ? [rng.Pick(members).Id] : [],
            TargetKind.RandomOther => PickOther(request.Subject, members, rng),
            _ => [],
        };
    }

    private static IReadOnlyList<PlayerId> PickOther(PlayerId? subject, IReadOnlyList<PartyMember> members, DeterministicRandom rng)
    {
        if (subject is not { } s)
        {
            return [];
        }

        var others = members.Where(m => m.Id != s).ToList();
        return others.Count > 0 ? [rng.Pick(others).Id] : [];
    }
}
