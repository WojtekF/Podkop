using Podkop.Statute.Domain;

namespace Podkop.Statute.Application;

public interface IStatuteRepository
{
    Task<IReadOnlyList<StatuteVersion>> GetAllVersionsAsync(CancellationToken cancellationToken);
}
