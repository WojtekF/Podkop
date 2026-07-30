using MediatR;

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
    public Task<FindingVoteResult> Handle(WithdrawFindingVote request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
