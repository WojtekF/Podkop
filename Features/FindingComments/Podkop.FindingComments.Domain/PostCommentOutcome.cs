namespace Podkop.FindingComments.Domain;

public enum PostCommentOutcome
{
    Posted,
    EmptyText,
    TextTooLong
}

/// <summary>
///     What <see cref="Comment.Post" /> produced: <see cref="PostCommentOutcome.Posted" /> with
///     the new aggregate, or a rejection carrying no comment at all.
/// </summary>
public sealed record PostCommentResult(PostCommentOutcome Outcome, Comment? Comment);
