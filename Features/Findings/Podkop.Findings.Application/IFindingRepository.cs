using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     The Findings slice's durable store seam (issue #67, ADR 0010), read/track-only since
///     issue #96. Reads hand back fully rehydrated aggregates — votes, tags, counts and
///     timestamps included — because every public fact (dig count, the reader's own vote, the
///     promotion state) is derived from what the aggregate holds. A loaded aggregate is
///     change-tracked: a handler calls its domain methods and the slice's
///     <see cref="IUnitOfWork" /> makes whatever was mutated durable in one explicit commit —
///     the repository itself persists nothing.
/// </summary>
public interface IFindingRepository
{
    /// <summary>
    ///     One page of the Main Page feed, paged in the store rather than in memory (issue #67):
    ///     promoted findings only, in feed order — newest promotion first, ties broken by id
    ///     descending (ADR 0004) — skipping the pages before <paramref name="page" /> (1-based)
    ///     and answering up to <paramref name="limit" /> + 1 findings, so the caller can tell a
    ///     full last page from one with a successor without a second query. A page past the end
    ///     answers empty.
    /// </summary>
    Task<IReadOnlyList<Finding>> GetPromotedPageAsync(int page, int limit, CancellationToken cancellationToken);

    Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    ///     The findings named by <paramref name="ids" />, in one round trip rather than one per
    ///     id (issue #77) — what tag-page hydration reads through. Promoted or not: this is a
    ///     lookup, not a feed. An id naming no finding contributes nothing, so the answer may be
    ///     shorter than the request and carries no ordering promise of its own; the caller
    ///     already knows the order it wants.
    /// </summary>
    Task<IReadOnlyList<Finding>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}
