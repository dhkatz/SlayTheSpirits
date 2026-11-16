using SlayTheSpirits.Party;
using SlayTheSpirits.Rules;
using SpireKit.Determinism;

namespace SlayTheSpirits.Engine;

/// <summary>Inputs available to every policy.</summary>
public sealed record PolicyContext(RuleSet Rules, RunPenaltyState State, IPartyView Party, DeterministicRandom Rng);

/// <summary>
/// One step of the assignment pipeline. Receives a resolved assignment and returns zero (cancel),
/// one (pass/modify) or more (split) assignments. Policies must be deterministic: use only
/// <see cref="PolicyContext.Rng"/> for randomness.
/// </summary>
public interface IPenaltyPolicy
{
    IEnumerable<PenaltyAssignment> Apply(PenaltyAssignment assignment, PolicyContext context);
}

/// <summary>
/// Passes a player's next penalty to a random other player (e.g. Slip Away). The rest of a redirected batch
/// (<see cref="PenaltyRequest.Batch"/>) follows it to the same player without using up another redirect.
/// </summary>
public sealed class RedirectPolicy : IPenaltyPolicy
{
    public IEnumerable<PenaltyAssignment> Apply(PenaltyAssignment assignment, PolicyContext context)
    {
        var state = context.State;
        if (assignment.OriginalPlayer is not null)
        {
            return [assignment];
        }

        if (assignment.Batch is { } batch
            && state.RedirectedBatches.TryGetValue(assignment.Player.Value, out var redirected)
            && redirected.Batch == batch)
        {
            return [Redirect(assignment, redirected.Target)];
        }

        if (!state.PendingRedirects.TryGetValue(assignment.Player.Value, out var remaining) || remaining <= 0)
        {
            return [assignment];
        }

        var others = context.Party.Members.Where(m => m.Id != assignment.Player).ToList();
        if (others.Count == 0)
        {
            return [assignment];
        }

        if (remaining == 1)
        {
            state.PendingRedirects.Remove(assignment.Player.Value);
        }
        else
        {
            state.PendingRedirects[assignment.Player.Value] = remaining - 1;
        }

        var target = context.Rng.Pick(others).Id;
        if (assignment.Batch is { } first)
        {
            state.RedirectedBatches[assignment.Player.Value] = (first, target);
        }

        return [Redirect(assignment, target)];
    }

    private static PenaltyAssignment Redirect(PenaltyAssignment assignment, PlayerId target) => assignment with
    {
        Player = target,
        OriginalPlayer = assignment.Player,
        Notes = [.. assignment.Notes, AssignmentNote.Redirected],
    };
}

/// <summary>Immune players (e.g. Designated Driver) skip alcoholic penalties; water still applies.</summary>
public sealed class ImmunityPolicy : IPenaltyPolicy
{
    public IEnumerable<PenaltyAssignment> Apply(PenaltyAssignment assignment, PolicyContext context) =>
        assignment.Penalty.IsAlcoholic && context.State.Immune.Contains(assignment.Player.Value)
            ? []
            : [assignment];
}

/// <summary>
/// Enforces <see cref="RuleSet.CapsPerAct"/>: a penalty that would push a player over the cap for its
/// kind is downgraded to the next lighter kind until it fits; if even a Sip doesn't fit it is dropped.
/// </summary>
public sealed class CapPolicy : IPenaltyPolicy
{
    public IEnumerable<PenaltyAssignment> Apply(PenaltyAssignment assignment, PolicyContext context)
    {
        var penalty = assignment.Penalty;
        if (!penalty.IsAlcoholic)
        {
            return [assignment];
        }

        var current = (Penalties.Penalty?)penalty;
        while (current is { } candidate
               && context.Rules.CapsPerAct.TryGetValue(candidate.Kind, out var cap)
               && context.State.CountThisAct(assignment.Player, candidate.Kind) + candidate.Count > cap)
        {
            current = candidate.Downgraded();
        }

        if (current is not { } final)
        {
            return [];
        }

        return final == penalty
            ? [assignment]
            : [assignment with
            {
                Penalty = final,
                OriginalPenalty = assignment.OriginalPenalty ?? penalty,
                Notes = [.. assignment.Notes, AssignmentNote.Downgraded],
            }];
    }
}
