namespace Podkop.FindingComments.Domain;

/// <summary>
///     Every way posting a comment can end (issue #17) — the one vocabulary shared by the
///     aggregate factory, the use-case handler, and the endpoint's ProblemDetails mapping.
///     <see cref="Comment.Post" /> produces the text and depth rejections; the lookup
///     rejections (<see cref="UnknownFinding" />, <see cref="UnknownParent" />) need a
///     repository, so the handler produces them.
/// </summary>
public enum PostCommentOutcome
{
    /// <summary>The comment was created — 201 with the created comment.</summary>
    Posted,

    /// <summary>Text empty after trimming — 400, <c>podkop:problem:comment-empty</c>.</summary>
    EmptyText,

    /// <summary>Text over the length cap — 400, <c>podkop:problem:comment-too-long</c>.</summary>
    TextTooLong,

    /// <summary>No finding has that id — 404, <c>podkop:problem:unknown-finding</c>.</summary>
    UnknownFinding,

    /// <summary>
    ///     No comment under the finding has the parent id — 404,
    ///     <c>podkop:problem:unknown-parent</c>.
    /// </summary>
    UnknownParent,

    /// <summary>The parent is itself a reply — 400, <c>podkop:problem:parent-is-a-reply</c>.</summary>
    ParentIsAReply
}

/// <summary>
///     What <see cref="Comment.Post" /> produced: <see cref="PostCommentOutcome.Posted" /> with
///     the new aggregate, or a rejection carrying no comment at all.
/// </summary>
public sealed record PostCommentResult(PostCommentOutcome Outcome, Comment? Comment);
