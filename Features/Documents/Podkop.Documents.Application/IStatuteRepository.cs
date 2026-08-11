using Podkop.Documents.Domain;

namespace Podkop.Documents.Application;

public interface IStatuteRepository
{
    Task<IReadOnlyList<StatuteVersion>> GetAllVersionsAsync(CancellationToken cancellationToken);
}
