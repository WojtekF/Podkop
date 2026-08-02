using MediatR;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     Command behind <c>PUT /api/findings/{id}/my-vote</c> (issue #15): an idempotent
///     set-my-vote covering fresh votes and one-click side switches alike. A dig carries no
///     reason; a bury must carry one of the five <see cref="BuryReason" /> values. The current
///     user comes from the <see cref="ICurrentUser" /> seam, never from the request.
/// </summary>
public sealed record SetFindingVote(Guid FindingId, FindingVoteSide Side, BuryReason? Reason) : IRequest<FindingVoteResult>;

/// <summary>
///     The fresh vote state of one finding after a mutation, for the frontend to reconcile from —
///     no refetch. Only the dig count is public: bury totals never appear in any response, so the
///     result carries no bury count at all. <c>MyVote</c> is <c>"dig"</c>, <c>"bury"</c>, or
///     <c>null</c> — the same values the finding detail carries.
/// </summary>
public sealed record FindingVotes(int DigCount, string? MyVote);

public enum FindingVoteError
{
    /// <summary>No finding has that id — the endpoint answers 404.</summary>
    UnknownFinding,

    /// <summary>The voter authored the finding — rejected — the endpoint answers 400.</summary>
    OwnFinding,

    /// <summary>A bury arrived without a reason — rejected — the endpoint answers 400.</summary>
    BuryReasonRequired
}

/// <summary>
///     Outcome of a finding-vote mutation: either <see cref="Error" /> is set and the endpoint
///     maps it to a status code, or <see cref="Votes" /> carries the finding's fresh state.
/// </summary>
public sealed record FindingVoteResult(FindingVoteError? Error, FindingVotes? Votes);

public sealed class SetFindingVoteHandler(
    IFindingRepository findingsRepository,
    ICurrentUser currentUser)
    : IRequestHandler<SetFindingVote, FindingVoteResult>
{
    public async Task<FindingVoteResult> Handle(SetFindingVote request, CancellationToken cancellationToken)
    {
        var finding = await findingsRepository.GetByIdAsync(request.FindingId, cancellationToken);
        if (finding is null)
        {
            return new FindingVoteResult(FindingVoteError.UnknownFinding,null);
        }

        var digBuryOutcome = finding.SetVote(currentUser.UserName, request.Side, request.Reason);
        if (digBuryOutcome == DigBuryOutcome.BuryReasonRequired)
        {
            return new FindingVoteResult(FindingVoteError.BuryReasonRequired, null);
        }

        if (digBuryOutcome == DigBuryOutcome.OwnFinding)
        {
            return new FindingVoteResult(FindingVoteError.OwnFinding, null);
        }

        return new FindingVoteResult(null, new FindingVotes(finding.DigCount, request.Side.ToApiString()));
    }
}
