using Podkop.Statute.Application;
using Podkop.Statute.Domain;

namespace Podkop.Statute.Infrastructure;

public sealed class InMemoryStatuteRepository(IReadOnlyList<StatuteVersion> versions) : IStatuteRepository
{
    public Task<IReadOnlyList<StatuteVersion>> GetAllVersionsAsync(CancellationToken cancellationToken)
        => Task.FromResult(versions);
}
