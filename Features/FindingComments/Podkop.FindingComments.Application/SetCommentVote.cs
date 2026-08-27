using MediatR;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Command behind <c>PUT /api/comments/{commentId}/my-vote</c> (issue #18): an idempotent
///     set-my-vote covering fresh votes and one-click side switches alike, mirroring the
///     finding-vote ruleset minus reasons. The current user comes from the
///     <see cref="ICurrentUser" /> seam, never from the request.
/// </summary>
public sealed record SetCommentVote(Guid CommentId, VoteDirection Direction) : IRequest<CommentVoteResult>;

/// <summary>
///     The fresh vote state of one comment after a mutation, for the frontend to reconcile
///     from — no refetch. <c>MyVote</c> is <c>"up"</c>, <c>"down"</c>, or <c>null</c>, the
///     same values the comment rows of <see cref="GetFindingComments" /> carry.
/// </summary>
public sealed record CommentVotes(int UpvoteCount, int DownvoteCount, string? MyVote);

public enum CommentVoteError
{
    /// <summary>No comment has that id — the endpoint answers 404.</summary>
    UnknownComment,

    /// <summary>The voter authored the comment — rejected — the endpoint answers 400.</summary>
    OwnComment
}

/// <summary>
///     Outcome of a comment-vote mutation: either <see cref="Error" /> is set and the endpoint
///     maps it to a status code, or <see cref="Votes" /> carries the comment's fresh state.
/// </summary>
public sealed record CommentVoteResult(CommentVoteError? Error, CommentVotes? Votes);

public sealed class SetCommentVoteHandler(
    ICommentRepository commentsRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetCommentVote, CommentVoteResult>
{
    public async Task<CommentVoteResult> Handle(SetCommentVote request, CancellationToken cancellationToken)
    {
        var comment = await commentsRepository.GetByIdAsync(request.CommentId, cancellationToken);
        if (comment is null) return new CommentVoteResult(CommentVoteError.UnknownComment, null);

        if (comment.SetVote(currentUser.UserName, request.Direction) == ActionOutcome.OwnComment)
            return new CommentVoteResult(CommentVoteError.OwnComment, null);

        await unitOfWork.CommitAsync(cancellationToken);

        return new CommentVoteResult(null, new CommentVotes(
            comment.UpvoteCount,
            comment.DownvoteCount,
            request.Direction.ToApiString()
        ));
    }
}
