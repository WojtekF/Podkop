using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.Server.Tests;

/// <summary>
///     The findings store, doubled at the slice's own port: findings live in PostgreSQL since
///     issue #67, so suites whose subject is the host's cross-slice wiring answer content facts
///     from a fixed list instead of hauling a database into specs that are not about
///     persistence. Feed order and the one-past-the-limit next-page signal mirror the durable
///     store's contract; saving is a no-op because mutations on the held aggregates are already
///     visible in place.
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

    public Task SaveAsync(Finding finding, CancellationToken cancellationToken) => Task.CompletedTask;
}
