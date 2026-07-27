using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Infrastructure;

public sealed class InMemoryFindingRepository(IEnumerable<Finding> findings) : IFindingRepository
{
    private readonly IReadOnlyList<Finding> _findings = findings.ToList();

    public Task<IReadOnlyList<Finding>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_findings);

    public Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_findings.FirstOrDefault(finding => finding.Id == id));
}
