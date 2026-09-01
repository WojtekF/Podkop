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

/// <summary>
///     The card data the Findings slice serves, wherever a finding is shown as a card: the Main
///     Page feed and — since issue #77 — the batch-by-ids hydration a tag page runs on (ADR
///     0011). One record for both, because it is literally the same card.
///     <para>
///         <see cref="PromotedAt" /> is nullable and <see cref="CreatedAt" /> always present
///         because of that second caller: the Main Page carries promoted findings only, but a tag
///         page carries every finding that took the tag, promoted or still upcoming, and an
///         upcoming finding has no promotion time to show. Created-at is the timestamp every card
///         can always fall back to.
///     </para>
/// </summary>
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
    DateTimeOffset CreatedAt,
    DateTimeOffset? PromotedAt);

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
            finding.CreatedAt,
            finding.PromotedAt);
    }
}