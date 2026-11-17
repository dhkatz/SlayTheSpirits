using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Engine;

/// <summary>Why a penalty happened: a trigger id or a card/relic id, plus optional detail for display.</summary>
/// <param name="SourceId">E.g. <c>encounter.boss.start</c> or <c>CARD.SPIRITS_LAST_CALL</c>.</param>
/// <param name="Label">E.g. "The Guardian" or "Last Call".</param>
public sealed record PenaltyCause(string SourceId, string? Label = null);

/// <summary>A request to hand out a penalty, before targets are resolved and policies applied.</summary>
/// <param name="Penalty">What to drink.</param>
/// <param name="Target">Who drinks, relative to <paramref name="Subject"/>.</param>
/// <param name="Subject">The player the request is about (the victim, or the card's chosen player).</param>
/// <param name="Instigator">The player who caused it (e.g. who played the card), if any. Used for "Others" from a card.</param>
/// <param name="Cause">Why.</param>
/// <param name="Voluntary">
/// The drinker chose it in exchange for a reward (a rest site drink, a Tavern potion). Policies never touch it: it
/// isn't redirected, skipped for immunity or downgraded by caps, since that would change the price of what they
/// chose. It still counts toward the caps for drinks they didn't choose.
/// </param>
/// <param name="Batch">
/// Groups the drinks from one card play ("take 2 Sips" is two drink cards, each played on its own). Drinks in one
/// batch are redirected together: if the first one is passed on, the rest follow it to the same player.
/// </param>
public sealed record PenaltyRequest(
    Penalty Penalty,
    TargetKind Target,
    PlayerId? Subject,
    PlayerId? Instigator,
    PenaltyCause Cause,
    bool Voluntary = false,
    string? Batch = null);

/// <summary>Annotations explaining how policies changed an assignment, for announcements and the bar tab.</summary>
public enum AssignmentNote
{
    /// <summary>Capped: downgraded from a heavier penalty.</summary>
    Downgraded,

    /// <summary>Passed on to this player by someone else's redirect.</summary>
    Redirected,
}

/// <summary>A final, resolved penalty for one player.</summary>
/// <param name="Player">Who drinks.</param>
/// <param name="Penalty">What, after policies.</param>
/// <param name="Cause">Why.</param>
/// <param name="Notes">How policies changed it (empty when untouched).</param>
/// <param name="OriginalPlayer">For redirected penalties, who it was originally for.</param>
/// <param name="OriginalPenalty">For downgraded penalties, what it was before capping.</param>
/// <param name="Tipsy">Tipsy the player gained from this penalty (set by the engine).</param>
/// <param name="Batch">The request's <see cref="PenaltyRequest.Batch"/>, if any.</param>
public sealed record PenaltyAssignment(
    PlayerId Player,
    Penalty Penalty,
    PenaltyCause Cause,
    IReadOnlyList<AssignmentNote> Notes,
    PlayerId? OriginalPlayer = null,
    Penalty? OriginalPenalty = null,
    int Tipsy = 0,
    string? Batch = null)
{
    public static PenaltyAssignment Plain(PlayerId player, Penalty penalty, PenaltyCause cause, string? batch = null) =>
        new(player, penalty, cause, [], Batch: batch);
}
