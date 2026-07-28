using MediatR;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Command behind <c>DELETE /api/comments/{commentId}/my-vote</c> (issue #18): withdraws
///     the current user's vote on the comment, freeing the count it was held in. The outcome
///     shape is shared with <see cref="SetCommentVote" />.
/// </summary>
public sealed record WithdrawCommentVote(Guid CommentId) : IRequest<CommentVoteResult>;

public sealed class WithdrawCommentVoteHandler(
    ICommentRepository commentsRepository,
    ICurrentUser currentUser)
    : IRequestHandler<WithdrawCommentVote, CommentVoteResult>
{
    public Task<CommentVoteResult> Handle(WithdrawCommentVote request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
