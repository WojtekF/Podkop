using MediatR;

namespace Podkop.Findings.Application;

/// <summary>
/// Query for one page of the Main Page feed: promoted findings only, ordered by
/// promotion time (newest first), positioned by an opaque <see cref="FeedCursor"/>.
/// </summary>
public sealed record GetMainPageFeed(string? Cursor, int Limit) : IRequest<FeedPage>;

public sealed record FeedPage(IReadOnlyList<FindingSummary> Items, string? NextCursor);

public sealed record FindingSummary(
    Guid Id,
    string Title,
    string Description,
    string SourceUrl,
    string Domain,
    string? ThumbnailUrl,
    string Author,
    IReadOnlyList<string> Tags,
    int DigCount,
    int CommentCount,
    DateTimeOffset PromotedAt);

public sealed class GetMainPageFeedHandler(IFindingRepository findings) : IRequestHandler<GetMainPageFeed, FeedPage>
{
    public Task<FeedPage> Handle(GetMainPageFeed request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Domain logic is implemented by the user (CLAUDE.md Feature Development Workflow).");
    }
}
