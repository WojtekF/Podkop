using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     The Findings slice's durable store seam (issue #67, ADR 0010). Reads hand back fully
///     rehydrated aggregates — votes, tags, counts and timestamps included — because every
///     public fact (dig count, the reader's own vote, the promotion state) is derived from what
///     the aggregate holds. Mutations follow the load-mutate-save shape: a handler loads the
///     finding, calls its domain methods, and makes the outcome durable through
///     <see cref="SaveAsync" /> — nothing persists on its own any more, the way the in-memory
///     singleton store used to make it look.
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
    ///     Makes the given finding's current state durable — the vote a handler just set or
    ///     withdrew, the comment count a contract event just moved — so the next request, in its
    ///     own scope, reads what this one changed.
    /// </summary>
    Task SaveAsync(Finding finding, CancellationToken cancellationToken);
}
