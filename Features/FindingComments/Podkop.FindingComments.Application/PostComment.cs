using MediatR;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Command behind <c>POST /api/findings/{findingId}/comments</c> (issue #17): posts a
///     top-level comment (<see cref="ParentCommentId" /> null) or a reply (set to a top-level
///     comment's id — a reply's parent can never itself be a reply, threads are one level deep).
///     The author is the current user from the <see cref="ICurrentUser" /> seam, never the
///     request. Text is trimmed before validation and storage.
/// </summary>
public sealed record PostComment(Guid FindingId, string? Text, Guid? ParentCommentId)
    : IRequest<PostCommentResponse>;

public enum PostCommentError
{
    /// <summary>No finding has that id — 404, <c>podkop:problem:unknown-finding</c>.</summary>
    UnknownFinding,

    /// <summary>No comment has the parent id — 404, <c>podkop:problem:unknown-parent</c>.</summary>
    UnknownParent,

    /// <summary>The parent is itself a reply — 400, <c>podkop:problem:parent-is-a-reply</c>.</summary>
    ParentIsAReply,

    /// <summary>Text empty after trimming — 400, <c>podkop:problem:comment-empty</c>.</summary>
    EmptyText,

    /// <summary>Text over the length cap — 400, <c>podkop:problem:comment-too-long</c>.</summary>
    TextTooLong
}

/// <summary>
///     Outcome of a post: either <see cref="Error" /> is set and the endpoint maps it to a
///     status code and problem type, or <see cref="Comment" /> carries the created comment in
///     the same shape a GET row has (<see cref="CommentReply" />), so the frontend renders it
///     straight from the response — no refetch.
/// </summary>
public sealed record PostCommentResponse(PostCommentError? Error, CommentReply? Comment);

public sealed class PostCommentHandler(
    ICommentRepository commentsRepository,
    IFindingLookup findingLookup,
    ICurrentUser currentUser)
    : IRequestHandler<PostComment, PostCommentResponse>
{
    public async Task<PostCommentResponse> Handle(PostComment request, CancellationToken cancellationToken)
    {
        if (!await findingLookup.ExistsAsync(request.FindingId, cancellationToken))
            return new PostCommentResponse(PostCommentError.UnknownFinding, null);

        if (request.ParentCommentId is not null)
        {
            var parentComment = await commentsRepository.GetByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment is null || parentComment.FindingId != request.FindingId)
                return new PostCommentResponse(PostCommentError.UnknownParent, null);
            if (parentComment.IsReply) return new PostCommentResponse(PostCommentError.ParentIsAReply, null);
        }

        var postCommentResult = Comment.Post(
            id: Guid.CreateVersion7(),
            request.FindingId,
            request.ParentCommentId,
            author: currentUser.UserName,
            request.Text,
            createdAt: DateTimeOffset.UtcNow);

        if (postCommentResult.Outcome == PostCommentOutcome.EmptyText)
            return new PostCommentResponse(PostCommentError.EmptyText, null);

        if (postCommentResult.Outcome == PostCommentOutcome.TextTooLong)
            return new PostCommentResponse(PostCommentError.TextTooLong, null);

        await commentsRepository.AddAsync(postCommentResult.Comment, cancellationToken);
        return new PostCommentResponse(null, postCommentResult.Comment.ToCommentReply(currentUser.UserName));
    }
}
