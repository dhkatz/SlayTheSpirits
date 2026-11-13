using SlayTheSpirits.Party;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using SpireKit.Determinism;

namespace SlayTheSpirits.Engine;

/// <summary>
/// The drinking game's brain for one run: occurrence → rule → targets → policies → recorded assignments.
/// </summary>
/// <remarks>
/// Deterministic given (rules, run seed, state, input order), so every peer computes identical results
/// without the engine ever sending a network message.
/// </remarks>
public sealed class PenaltyEngine
{
    /// <summary>Name of the RNG stream derived from the run seed. Changing it changes every random pick.</summary>
    public const string RngStream = "SlayTheSpirits/penalties";

    private readonly ulong _streamSeed;
    private readonly IReadOnlyList<IPenaltyPolicy> _policies;
    private readonly DeterministicRandom _rng;

    public PenaltyEngine(RuleSet rules, RunPenaltyState state, ulong runSeed, IReadOnlyList<IPenaltyPolicy>? policies = null)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        State = state ?? throw new ArgumentNullException(nameof(state));
        _policies = policies ?? DefaultPolicies;
        _streamSeed = StableHash.Combine(runSeed, StableHash.Fnv1a64(RngStream));
        _rng = DeterministicRandom.Restore(_streamSeed, state.RngDraws);
    }

    /// <summary>Redirect, then immunity, then caps.</summary>
    public static IReadOnlyList<IPenaltyPolicy> DefaultPolicies { get; } = [new RedirectPolicy(), new ImmunityPolicy(), new CapPolicy()];

    public RuleSet Rules { get; }

    public RunPenaltyState State { get; }

    /// <summary>Applies the rules to a game moment.</summary>
    public IReadOnlyList<PenaltyAssignment> Handle(TriggerOccurrence occurrence, IPartyView party) =>
        RuleResolver.Resolve(Rules, occurrence) is { } request ? Handle(request, party) : [];

    /// <summary>Hands out an explicit penalty (cards, relics, potions).</summary>
    public IReadOnlyList<PenaltyAssignment> Handle(PenaltyRequest request, IPartyView party)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(party);

        var context = new PolicyContext(Rules, State, party, _rng);
        var results = new List<PenaltyAssignment>();
        foreach (var player in TargetResolver.Resolve(request, party, _rng))
        {
            IEnumerable<PenaltyAssignment> pending = [PenaltyAssignment.Plain(player, request.Penalty, request.Cause)];
            foreach (var policy in _policies)
            {
                pending = [.. pending.SelectMany(a => policy.Apply(a, context))];
            }

            foreach (var resolved in pending)
            {
                var assignment = resolved with { Tipsy = TipsyWeights.For(Rules, resolved.Penalty) };
                State.Record(assignment.Player, assignment.Penalty);
                State.Tally.Add(assignment);
                results.Add(assignment);
            }
        }

        State.RngDraws = _rng.Draws;
        return results;
    }

    /// <summary>Moves to a new act, resetting per-act caps.</summary>
    public void EnterAct(int act)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(act, 1);
        if (act == State.Act)
        {
            return;
        }

        State.Act = act;
        State.ActCounts.Clear();
    }

    /// <summary>Run-long Tipsy for a player (what their Tipsy buff should show).</summary>
    public int TipsyOf(PlayerId player) => State.Tally.TipsyOf(player);

    public void GrantImmunity(PlayerId player) => State.Immune.Add(player.Value);

    public void ClearImmunities() => State.Immune.Clear();

    public void AddRedirects(PlayerId player, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        State.PendingRedirects[player.Value] = State.PendingRedirects.GetValueOrDefault(player.Value) + count;
    }
}
