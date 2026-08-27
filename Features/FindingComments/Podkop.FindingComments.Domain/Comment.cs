namespace Podkop.FindingComments.Domain;

/// <summary>
///     A user-authored text response attached to a finding — the unit of discussion (CONTEXT.md).
///     Comment is its own aggregate referencing the finding (and, when it is a reply, its parent
///     comment) by id only; it is never held as a collection on the finding (ADR 0005). A reply
///     can never have replies — threads are exactly one level deep.
/// </summary>
public sealed class Comment
{
    /// <summary>The most text one comment may carry (issue #17); longer text is rejected.</summary>
    public const int MaxTextLength = 5000;

    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<CommentVoteEntry> _votes = [];

    public Comment(
        Guid id,
        Guid findingId,
        Guid? parentCommentId,
        string author,
        string text,
        DateTimeOffset createdAt,
        IReadOnlyDictionary<string, VoteDirection>? votes = null)
    {
        Id = id;
        FindingId = findingId;
        ParentCommentId = parentCommentId;
        Author = author;
        Text = text;
        CreatedAt = createdAt;
        _votes = votes is null ? [] : votes.Select(vote => new CommentVoteEntry(vote.Key, vote.Value)).ToList();
    }

    private Comment()
    {
    }

    public Guid Id { get; }
    public Guid FindingId { get; }
    public Guid? ParentCommentId { get; }
    public string Author { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }
    public int UpvoteCount => _votes.Count(vote => vote.VoteDirection == VoteDirection.Up);
    public int DownvoteCount => _votes.Count(vote => vote.VoteDirection == VoteDirection.Down);

    /// <summary>
    ///     The individual votes tracked per voter. Tracked voter can have their
    ///     vote highlighted, switched, or withdrawn.
    /// </summary>
    public IReadOnlyList<CommentVoteEntry> Votes => _votes;

    public bool IsReply => ParentCommentId is not null;
    public int NetScore => UpvoteCount - DownvoteCount;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    ///     Creates a newly posted comment (issue #17). A reply's parent is passed in loaded so
    ///     the factory owns the depth invariant — a reply can never answer a reply; whether the
    ///     parent exists at all is a lookup, checked where the repository is available. Text is
    ///     trimmed before validation and storage; text that is empty after trimming is rejected,
    ///     as is text longer than <see cref="MaxTextLength" /> characters. A successful post
    ///     raises <see cref="CommentAdded" />.
    /// </summary>
    public static PostCommentResult Post(
        Guid id,
        Guid findingId,
        Comment? parent,
        string author,
        string? text,
        DateTimeOffset createdAt)
    {
        if (parent is { IsReply: true }) return new PostCommentResult(PostCommentOutcome.ParentIsAReply, null);

        if (string.IsNullOrWhiteSpace(text)) return new PostCommentResult(PostCommentOutcome.EmptyText, null);

        var trimmedText = text.Trim();
        if (trimmedText.Length > MaxTextLength) return new PostCommentResult(PostCommentOutcome.TextTooLong, null);

        var comment = new Comment(
            id,
            findingId,
            parent?.Id,
            author,
            trimmedText,
            createdAt);

        comment.RaiseCommentAdded();
        return new PostCommentResult(
            PostCommentOutcome.Posted,
            comment
        );
    }

    private void RaiseCommentAdded() => _domainEvents.Add(new CommentAdded(Id, FindingId));

    /// <summary>
    ///     Records the voter's vote (issue #18): a fresh vote or a one-step switch to the other
    ///     side; setting the side already held changes nothing. The counts and the tracked votes
    ///     must stay consistent with each other, and the comment's own author can never vote —
    ///     the same ruleset as finding votes, minus reasons.
    /// </summary>
    public ActionOutcome SetVote(string voter, VoteDirection direction)
    {
        if (voter == Author) return ActionOutcome.OwnComment;

        _votes.RemoveAll(vote => vote.Voter == voter);
        _votes.Add(new CommentVoteEntry(voter, direction));

        return ActionOutcome.Applied;
    }

    /// <summary>
    ///     Withdraws the voter's vote (issue #18), freeing the count it was held in.
    /// </summary>
    public ActionOutcome WithdrawVote(string voter)
    {
        if (voter == Author) return ActionOutcome.OwnComment;
        _votes.RemoveAll(vote => vote.Voter == voter);
        return ActionOutcome.Applied;
    }

    public VoteDirection? VoteBy(string voter) => Votes.FirstOrDefault(vote => vote.Voter == voter)?.VoteDirection;
}
