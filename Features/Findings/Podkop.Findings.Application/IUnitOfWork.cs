namespace Podkop.Findings.Application;

/// <summary>
///     The Findings slice's commit seam (issue #96): a use case makes its work durable by
///     committing exactly once, explicitly, after its mutations — never by expecting a load or a
///     repository call to persist anything on its own. The unit of work spans this slice's own
///     store only (ADR 0010): cross-slice effects stay eventually consistent through contract
///     events, never one transaction across schemas.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    ///     Makes every mutation this use case performed on loaded aggregates durable in one
    ///     commit, so the next request — in its own scope — reads what this one changed.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
