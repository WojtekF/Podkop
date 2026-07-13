using MediatR;

namespace Podkop.Findings.Application;

/// <summary>
///     Query for one page of the Main Page feed: promoted findings only, ordered by
///     promotion time (newest first), addressed by a 1-based page number (ADR 0004).
///     A page past the end of the feed yields an empty page, not an error.
/// </summary>
public sealed record GetMainPageFeed(int Page, int Limit) : IRequest<FeedPage>;

public sealed record FeedPage(IReadOnlyList<FindingSummary> Items, bool HasNextPage);

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

public sealed class GetMainPageFeedHandler(IFindingRepository findingsRepository)
    : IRequestHandler<GetMainPageFeed, FeedPage>
{
    public Task<FeedPage> Handle(GetMainPageFeed request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Feed paging logic is implemented by the user (CLAUDE.md Feature Development Workflow).");
    }
}
