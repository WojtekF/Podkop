using Podkop.Documents.Application;
using Podkop.Documents.Domain;

namespace Podkop.Documents.Infrastructure;

public sealed class InMemoryStatuteRepository(IReadOnlyList<StatuteVersion> versions) : IStatuteRepository
{
    public Task<IReadOnlyList<StatuteVersion>> GetAllVersionsAsync(CancellationToken cancellationToken)
        => Task.FromResult(versions);
}
