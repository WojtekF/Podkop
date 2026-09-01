using MediatR;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     Query for a batch of findings addressed by id — the obligation joining the tag namespace
///     puts on a content slice (ADR 0011): a tag page serves typed references, and the frontend
///     hydrates the finding-shaped ones through here, in one call per page rather than one per
///     card.
///     <para>
///         It answers the same <see cref="FindingSummary" /> the feed serves, so a hydrated card
///         and a feed card are the same card. It is a lookup, not a feed: promoted and upcoming
///         findings alike come back, because a tag page lists everything that took the tag. Ids
///         naming no finding are simply absent from the answer rather than an error — a
///         reference whose content has just vanished hydrates to nothing and the page drops it
///         (ADR 0011) — so the answer may be shorter than the request, and the caller, which
///         already knows the order it wants, is the one that puts the results back in it.
///     </para>
/// </summary>
public sealed record GetFindingsByIds(IReadOnlyList<Guid> Ids) : IRequest<IReadOnlyList<FindingSummary>>;

public sealed class GetFindingsByIdsHandler(IFindingRepository findingsRepository)
    : IRequestHandler<GetFindingsByIds, IReadOnlyList<FindingSummary>>
{
    public Task<IReadOnlyList<FindingSummary>> Handle(
        GetFindingsByIds request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
