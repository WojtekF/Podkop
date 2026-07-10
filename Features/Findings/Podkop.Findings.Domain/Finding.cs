namespace Podkop.Findings.Domain;

public sealed class Finding
{
    private readonly List<IDomainEvent> _domainEvents = [];

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
        int commentCount)
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
    public int DigCount { get; }
    public int BuryCount { get; }
    public int CommentCount { get; }

    public bool IsPromoted => PromotedAt is not null;
    public int NetScore => DigCount - BuryCount;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// One-way promotion to the Main Page (ADR 0001): stamps <see cref="PromotedAt"/> and raises
    /// <see cref="FindingPromoted"/>. Idempotent — promoting an already-promoted finding changes
    /// nothing and raises no second event.
    /// </summary>
    public void Promote(DateTimeOffset promotedAt)
    {
        throw new NotImplementedException("Domain logic is implemented by the user (CLAUDE.md Feature Development Workflow).");
    }
}
