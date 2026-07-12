using MediatR;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     Query for one page of the Main Page feed: promoted findings only, ordered by
///     promotion time (newest first), positioned by an opaque <see cref="FeedCursor" />.
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

public sealed class GetMainPageFeedHandler(IFindingRepository findingsRepository)
    : IRequestHandler<GetMainPageFeed, FeedPage>
{
    public async Task<FeedPage> Handle(GetMainPageFeed request, CancellationToken cancellationToken)
    {
        if (!FeedCursor.TryDecode(request.Cursor!, out var lastPromotedAt, out var lastFindingGuid))
            return new FeedPage(Array.Empty<FindingSummary>(), null);
        var findings = await GetPromotedFindings(cancellationToken);
        var lastItemIndexed = findings
            .Index()
            .Where(t => t.Item.Id == lastFindingGuid && t.Item.PromotedAt == lastPromotedAt);

        if (lastFindingGuid != Guid.Empty)
        {
            var lastItemIndex = lastItemIndexed.First().Index + 1;
            findings = findings
                .Skip(lastItemIndex).ToList();
        }

        var nextBatch = findings
            .Take(request.Limit)
            .Select(x =>
                new FindingSummary(x.Id,
                    x.Title,
                    x.Description,
                    x.Source.AbsoluteUri,
                    x.Source.Host,
                    x.Thumbnail?.AbsoluteUri,
                    x.Author,
                    x.Tags,
                    x.DigCount,
                    x.CommentCount,
                    x.PromotedAt!.Value))
            .ToList();

        var newLastItem = nextBatch.LastOrDefault();
        string? newCursor = null;
        if (nextBatch.Count == request.Limit && newLastItem is not null)
            newCursor = FeedCursor.Encode(newLastItem.PromotedAt, newLastItem.Id);

        return new FeedPage(nextBatch, newCursor);
    }

    private async Task<IReadOnlyList<Finding>> GetPromotedFindings(CancellationToken cancellationToken)
    {
        var findings = await findingsRepository.GetAllAsync(cancellationToken);
        return findings.Where(x => x.IsPromoted).OrderByDescending(x => x.PromotedAt).ToList();
    }
}