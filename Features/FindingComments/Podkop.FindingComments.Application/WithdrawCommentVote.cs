using MediatR;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Command behind <c>DELETE /api/comments/{commentId}/my-vote</c> (issue #18): withdraws
///     the current user's vote on the comment, freeing the count it was held in. The outcome
///     shape is shared with <see cref="SetCommentVote" />.
/// </summary>
public sealed record WithdrawCommentVote(Guid CommentId) : IRequest<CommentVoteResult>;

public sealed class WithdrawCommentVoteHandler(
    ICommentRepository commentsRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<WithdrawCommentVote, CommentVoteResult>
{
    public async Task<CommentVoteResult> Handle(WithdrawCommentVote request, CancellationToken cancellationToken)
    {
        var comment = await commentsRepository.GetByIdAsync(request.CommentId, cancellationToken);

        if (comment is null) return new CommentVoteResult(CommentVoteError.UnknownComment, null);

        if (comment.WithdrawVote(currentUser.UserName) == ActionOutcome.OwnComment)
            return new CommentVoteResult(CommentVoteError.OwnComment, null);

        await unitOfWork.CommitAsync(cancellationToken);

        return new CommentVoteResult(null, new CommentVotes(comment.UpvoteCount, comment.DownvoteCount, null));
    }
}
