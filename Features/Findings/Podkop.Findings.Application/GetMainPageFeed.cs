using MediatR;
using Podkop.Findings.Domain;

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
    public async Task<FeedPage> Handle(GetMainPageFeed request, CancellationToken cancellationToken)
    {
        // Filtering, ordering, and paging live in the repository since issue #67 (SQL paging,
        // ADR 0004); the extra finding beyond the limit is the repository's next-page signal.
        var findings = await findingsRepository.GetPromotedPageAsync(
            request.Page, request.Limit, cancellationToken);
        var findingSummary = findings.Select(MapFindingToFindingSummary).ToList();
        return new FeedPage(findingSummary.Take(request.Limit).ToList(),
            findingSummary.Count > request.Limit);
    }

    private static FindingSummary MapFindingToFindingSummary(Finding finding)
    {
        return new FindingSummary(
            finding.Id,
            finding.Title,
            finding.Description,
            finding.Source.AbsoluteUri,
            finding.Source.Host,
            finding.Thumbnail?.AbsoluteUri,
            finding.Author,
            finding.Tags,
            finding.DigCount,
            finding.CommentCount,
            finding.PromotedAt!.Value);
    }
}