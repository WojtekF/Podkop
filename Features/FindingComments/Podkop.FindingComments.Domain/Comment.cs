namespace Podkop.FindingComments.Domain;

/// <summary>
/// A user-authored text response attached to a finding — the unit of discussion (CONTEXT.md).
/// Comment is its own aggregate referencing the finding (and, when it is a reply, its parent
/// comment) by id only; it is never held as a collection on the finding (ADR 0005). A reply
/// can never have replies — threads are exactly one level deep.
/// </summary>
public sealed class Comment
{
    private readonly Dictionary<string, VoteDirection> _votes;

    public Comment(
        Guid id,
        Guid findingId,
        Guid? parentCommentId,
        string author,
        string text,
        DateTimeOffset createdAt,
        int upvoteCount,
        int downvoteCount,
        IReadOnlyDictionary<string, VoteDirection>? votes = null)
    {
        Id = id;
        FindingId = findingId;
        ParentCommentId = parentCommentId;
        Author = author;
        Text = text;
        CreatedAt = createdAt;
        UpvoteCount = upvoteCount;
        DownvoteCount = downvoteCount;
        _votes = votes is null ? [] : new Dictionary<string, VoteDirection>(votes);
    }

    public Guid Id { get; }
    public Guid FindingId { get; }
    public Guid? ParentCommentId { get; }
    public string Author { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    public int UpvoteCount { get; private set; }
    public int DownvoteCount { get; private set; }

    /// <summary>
    ///     The individual votes tracked per voter. Seeded counts may include votes from users
    ///     whose individual records were never tracked — only a tracked voter can have their
    ///     vote highlighted, switched, or withdrawn.
    /// </summary>
    public IReadOnlyDictionary<string, VoteDirection> Votes => _votes;

    public bool IsReply => ParentCommentId is not null;
    public int NetScore => UpvoteCount - DownvoteCount;

    /// <summary>
    ///     Records the voter's vote (issue #18): a fresh vote or a one-step switch to the other
    ///     side; setting the side already held changes nothing. The counts and the tracked votes
    ///     must stay consistent with each other, and the comment's own author can never vote —
    ///     the same ruleset as finding votes, minus reasons.
    /// </summary>
    public void SetVote(string voter, VoteDirection direction)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Withdraws the voter's vote (issue #18), freeing the count it was held in.
    /// </summary>
    public void WithdrawVote(string voter)
    {
        throw new NotImplementedException();
    }
}
