using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

public interface IFindingRepository
{
    Task<IReadOnlyList<Finding>> GetAllAsync(CancellationToken cancellationToken);
}
