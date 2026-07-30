using MediatR;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     Query for a single finding addressed by its id, used by the finding detail page. Yields
///     <c>null</c> when no finding has that id so the endpoint can answer 404. The projection
///     deliberately omits the bury count: bury totals are never public (CONTEXT.md), so the DTO
///     carries no field for them at all. It does carry the current user's vote —
///     <c>"dig"</c>, <c>"bury"</c>, or <c>null</c> — so the reader's highlight survives a page
///     reload (issue #15).
/// </summary>
public sealed record GetFindingDetail(Guid Id) : IRequest<FindingDetail?>;

public sealed record FindingDetail(
    Guid Id,
    string Title,
    string Description,
    string SourceUrl,
    string Domain,
    string? ThumbnailUrl,
    string Author,
    IReadOnlyList<string> Tags,
    int DigCount,
    string? MyVote,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PromotedAt);

public sealed class GetFindingDetailHandler(IFindingRepository findingsRepository)
    : IRequestHandler<GetFindingDetail, FindingDetail?>
{
    public async Task<FindingDetail?> Handle(GetFindingDetail request, CancellationToken cancellationToken)
    {
        var finding = await findingsRepository.GetByIdAsync(request.Id, cancellationToken);
        return MapToFindingDetail(finding);
    }

    private static FindingDetail? MapToFindingDetail(Finding? finding)
    {
        return finding is null
            ? null
            : new FindingDetail(
                finding.Id,
                finding.Title,
                finding.Description,
                finding.Source.AbsoluteUri,
                finding.Source.Host,
                finding.Thumbnail?.AbsoluteUri,
                finding.Author,
                finding.Tags,
                finding.DigCount,
                // The reader's own vote is not wired up yet: the detail reports null until the
                // finding-vote logic (and the seeded stub votes) exist (issue #15).
                MyVote: null,
                finding.CommentCount,
                finding.CreatedAt,
                finding.PromotedAt);
    }
}