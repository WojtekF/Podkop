namespace Podkop.FindingComments.Domain;

/// <summary>
/// A user-authored text response attached to a finding — the unit of discussion (CONTEXT.md).
/// Comment is its own aggregate referencing the finding (and, when it is a reply, its parent
/// comment) by id only; it is never held as a collection on the finding (ADR 0005). A reply
/// can never have replies — threads are exactly one level deep.
/// </summary>
public sealed class Comment
{
    public Comment(
        Guid id,
        Guid findingId,
        Guid? parentCommentId,
        string author,
        string text,
        DateTimeOffset createdAt,
        int upvoteCount,
        int downvoteCount)
    {
        Id = id;
        FindingId = findingId;
        ParentCommentId = parentCommentId;
        Author = author;
        Text = text;
        CreatedAt = createdAt;
        UpvoteCount = upvoteCount;
        DownvoteCount = downvoteCount;
    }

    public Guid Id { get; }
    public Guid FindingId { get; }
    public Guid? ParentCommentId { get; }
    public string Author { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    public int UpvoteCount { get; }
    public int DownvoteCount { get; }

    public bool IsReply => ParentCommentId is not null;
    public int NetScore => UpvoteCount - DownvoteCount;
}
