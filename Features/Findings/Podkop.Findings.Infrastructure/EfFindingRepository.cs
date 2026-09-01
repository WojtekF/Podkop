using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IFindingRepository" /> (issue #67): findings live in the
///     slice's PostgreSQL schema, reached through <see cref="FindingsDbContext" />. The feed page
///     is composed by the database — promoted findings only, newest promotion first with id
///     descending as the tiebreak, the pages before the asked-for one skipped, and up to one
///     finding beyond the limit as the next-page signal — never by loading the whole table and
///     paging in memory (issue #67's SQL paging, ADR 0004). The id lookup answers the one finding
///     with that id, rehydrated whole: its votes, tags, counts and timestamps all read back
///     exactly as they were saved, because dig counts and the reader's own vote are derived from
///     them; it answers null when no finding has the id. The loaded aggregate stays tracked by
///     the request's context (issue #96): the repository persists nothing itself — durability is
///     <see cref="EfUnitOfWork" />'s single explicit commit. The specs in
///     <c>Podkop.Findings.Tests</c> pin both reads against the live database.
/// </summary>
public sealed class EfFindingRepository(FindingsDbContext context) : IFindingRepository
{
    public async Task<IReadOnlyList<Finding>> GetPromotedPageAsync(
        int page, int limit, CancellationToken cancellationToken) =>
        await context.Findings
            .Where(finding => finding.PromotedAt.HasValue)
            .OrderByDescending(finding => finding.PromotedAt.Value)
            .ThenByDescending(finding => finding.Id)
            .Skip((page - 1) * limit)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

    public Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Findings.FirstOrDefaultAsync(finding => finding.Id == id, cancellationToken);

    public Task<IReadOnlyList<Finding>> GetByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
