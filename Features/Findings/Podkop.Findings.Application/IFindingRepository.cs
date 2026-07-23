using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

public interface IFindingRepository
{
    Task<IReadOnlyList<Finding>> GetAllAsync(CancellationToken cancellationToken);

    Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
