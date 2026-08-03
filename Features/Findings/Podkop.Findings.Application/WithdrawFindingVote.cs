using MediatR;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     Command behind <c>DELETE /api/findings/{id}/my-vote</c> (issue #15): withdraws the current
///     user's vote on the finding, freeing the count it was held in. The outcome shape is shared
///     with <see cref="SetFindingVote" />.
/// </summary>
public sealed record WithdrawFindingVote(Guid FindingId) : IRequest<FindingVoteResult>;

public sealed class WithdrawFindingVoteHandler(
    IFindingRepository findingsRepository,
    ICurrentUser currentUser)
    : IRequestHandler<WithdrawFindingVote, FindingVoteResult>
{
    public async Task<FindingVoteResult> Handle(WithdrawFindingVote request, CancellationToken cancellationToken)
    {
        var finding = await findingsRepository.GetByIdAsync(request.FindingId, cancellationToken);

        if (finding is null) return new FindingVoteResult(FindingVoteError.UnknownFinding, null);

        var withdrawOutcome = finding.WithdrawVote(currentUser.UserName);
        if (withdrawOutcome == WithdrawOutcome.OwnFinding)
            return new FindingVoteResult(FindingVoteError.OwnFinding, null);

        return new FindingVoteResult(null,
            new FindingVotes(finding.DigCount, finding.VoteBy(currentUser.UserName).ToApiString()));
    }
}
