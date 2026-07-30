namespace Podkop.Findings.Domain;

public sealed class Finding
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly Dictionary<string, FindingVote> _votes;

    public Finding(
        Guid id,
        string title,
        string description,
        Uri source,
        Uri? thumbnail,
        string author,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset? promotedAt,
        int digCount,
        int buryCount,
        int commentCount,
        IReadOnlyDictionary<string, FindingVote>? votes = null)
    {
        Id = id;
        Title = title;
        Description = description;
        Source = source;
        Thumbnail = thumbnail;
        Author = author;
        Tags = tags;
        CreatedAt = createdAt;
        PromotedAt = promotedAt;
        DigCount = digCount;
        BuryCount = buryCount;
        CommentCount = commentCount;
        _votes = votes is null ? [] : new Dictionary<string, FindingVote>(votes);
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Description { get; }
    public Uri Source { get; }
    public Uri? Thumbnail { get; }
    public string Author { get; }
    public IReadOnlyList<string> Tags { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? PromotedAt { get; private set; }
    public int DigCount { get; private set; }
    public int BuryCount { get; private set; }
    public int CommentCount { get; private set; }

    public bool IsPromoted => PromotedAt is not null;
    public int NetScore => DigCount - BuryCount;

    /// <summary>
    ///     The finding votes tracked per voter (issue #15). Seeded counts may include votes from
    ///     users whose individual records were never tracked — only a tracked voter can have
    ///     their vote highlighted, switched, or withdrawn.
    /// </summary>
    public IReadOnlyDictionary<string, FindingVote> Votes => _votes;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    ///     One-way promotion to the Main Page (ADR 0001): stamps <see cref="PromotedAt" /> and raises
    ///     <see cref="FindingPromoted" />. Idempotent — promoting an already-promoted finding changes
    ///     nothing and raises no second event.
    /// </summary>
    public void Promote(DateTimeOffset promotedAt)
    {
        if (IsPromoted) return;
        PromotedAt = promotedAt;
        _domainEvents.Add(new FindingPromoted(Id, promotedAt));
    }

    /// <summary>
    ///     Records the voter's vote (issue #15): a fresh dig or bury, or a one-click switch to the
    ///     other side; setting the side already held changes nothing. A bury must carry a
    ///     <see cref="BuryReason" />; a bury without one is rejected. The finding's own author can
    ///     never vote on it, so scores can't be self-inflated. The dig and bury counts and the
    ///     tracked votes must stay consistent with each other; the bury count never leaves the
    ///     aggregate.
    /// </summary>
    public void SetVote(string voter, FindingVoteSide side, BuryReason? reason)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Withdraws the voter's vote (issue #15), freeing the count it was held in.
    /// </summary>
    public void WithdrawVote(string voter)
    {
        throw new NotImplementedException();
    }

    // method exposed for seeding purpose only.
    public void UpdateCommentCount(int commentCount)
    {
        CommentCount = commentCount;
    }
}