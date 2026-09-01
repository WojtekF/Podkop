namespace Podkop.Tags.Application;

/// <summary>
///     The Tags slice's commit seam (issue #96's pattern): a use case makes its work durable by
///     committing exactly once, explicitly, after its mutations — never by expecting a load or a
///     repository call to persist anything on its own. The unit of work spans this slice's own
///     store only (ADR 0010): the index is eventually consistent with the content slices it
///     mirrors, by contract events, never by one transaction across schemas.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    ///     Makes every mutation this use case performed durable in one commit, so the next
    ///     request — in its own scope — reads what this one changed.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
