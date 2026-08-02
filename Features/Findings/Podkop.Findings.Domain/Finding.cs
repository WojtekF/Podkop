namespace Podkop.Findings.Domain;

public sealed class Finding
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly Dictionary<string, FindingVote> _votes;

    public Finding(Guid id,
        string title,
        string description,
        Uri source,
        Uri? thumbnail,
        string author,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset? promotedAt,
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
    public int DigCount => _votes.Values.Count(vote => vote.Side == FindingVoteSide.Dig);
    public int BuryCount => _votes.Values.Count(vote => vote.Side == FindingVoteSide.Bury);
    public int CommentCount { get; private set; }

    public bool IsPromoted => PromotedAt is not null;
    public int NetScore => DigCount - BuryCount;

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
    public DigBuryOutcome SetVote(string voter, FindingVoteSide side, BuryReason? reason)
    {
        if (voter == Author) return DigBuryOutcome.OwnFinding;
        if (side == FindingVoteSide.Bury && reason is null) return DigBuryOutcome.BuryReasonRequired;
        _votes[voter] = new FindingVote(side, reason);
        return DigBuryOutcome.Applied;
    }


    /// <summary>
    ///     Withdraws the voter's vote (issue #15), freeing the count it was held in.
    /// </summary>
    public WithdrawOutcome WithdrawVote(string voter)
    {
        if (voter == Author) return WithdrawOutcome.OwnFinding;

        _votes.Remove(voter);
        return WithdrawOutcome.Applied;
    }

    // method exposed for seeding purpose only.
    public void UpdateCommentCount(int commentCount)
    {
        CommentCount = commentCount;
    }

    public string? VoteBy(string voter)
    {
        return _votes.TryGetValue(voter, out var value) ? value!.Side.ToApiString() : null;
    }
}
