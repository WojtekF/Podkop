namespace Podkop.FindingComments.Application;

/// <summary>
///     The FindingComments slice's commit seam (issue #68, patterned on issue #96): a use case
///     makes its work durable by committing exactly once, explicitly, after its mutations —
///     never by expecting a load, an add, or any repository call to persist anything on its own.
///     The unit of work spans this slice's own store only (ADR 0010): cross-slice effects stay
///     eventually consistent through contract events, never one transaction across schemas.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    ///     Makes every mutation this use case performed — comments added, votes set or withdrawn
    ///     on loaded comments — durable in one commit, so the next request, in its own scope,
    ///     reads what this one changed.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
