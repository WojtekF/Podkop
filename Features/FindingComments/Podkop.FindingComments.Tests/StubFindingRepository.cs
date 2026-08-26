using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The findings store, doubled at the Findings slice's own port: findings live in PostgreSQL
///     since issue #67, so suites whose subject is the discussion — not findings persistence —
///     hang their seeded threads off a fixed finding instead of hauling a database in. Feed order
///     and the one-past-the-limit next-page signal mirror the durable store's contract; mutations
///     on the held aggregates (the comment count the contract event moves) are already visible in
///     place, which is why the paired <see cref="StubUnitOfWork" /> has nothing left to commit
///     (issue #96).
/// </summary>
internal sealed class StubFindingRepository(IEnumerable<Finding> findings) : IFindingRepository
{
    private readonly IReadOnlyList<Finding> _findings = findings.ToList();

    public Task<IReadOnlyList<Finding>> GetPromotedPageAsync(
        int page, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Finding>>(_findings
            .Where(finding => finding.IsPromoted)
            .OrderByDescending(finding => finding.PromotedAt)
            .ThenByDescending(finding => finding.Id)
            .Skip((page - 1) * limit)
            .Take(limit + 1)
            .ToList());

    public Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_findings.FirstOrDefault(finding => finding.Id == id));
}
