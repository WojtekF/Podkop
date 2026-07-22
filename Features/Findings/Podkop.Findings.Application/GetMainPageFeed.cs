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
        var findings = await findingsRepository.GetAllAsync(cancellationToken);
        var findingSummary = findings
            .Where(f => f.IsPromoted)
            .OrderByDescending(f => f.PromotedAt)
            .ThenByDescending(f => f.Id)
            .Skip((request.Page - 1) * request.Limit)
            .Take(request.Limit + 1)
            .Select(MapFindingToFindingSummary)
            .ToList();
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