using MediatR;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     Query for a single finding addressed by its id, used by the read-only finding
///     detail page. Yields <c>null</c> when no finding has that id so the endpoint can
///     answer 404. The projection deliberately omits the bury count: bury totals are
///     never public (CONTEXT.md), so the DTO carries no field for them at all.
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
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PromotedAt);

public sealed class GetFindingDetailHandler(IFindingRepository findingsRepository)
    : IRequestHandler<GetFindingDetail, FindingDetail?>
{
    public Task<FindingDetail?> Handle(GetFindingDetail request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
