using MediatR;

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
    public Task<PostCommentResponse> Handle(PostComment request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
