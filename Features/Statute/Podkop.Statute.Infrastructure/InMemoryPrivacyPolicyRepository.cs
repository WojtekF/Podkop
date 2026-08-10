using Podkop.Statute.Application;
using Podkop.Statute.Domain;

namespace Podkop.Statute.Infrastructure;

public sealed class InMemoryPrivacyPolicyRepository(IReadOnlyList<PrivacyPolicyVersion> versions)
    : IPrivacyPolicyRepository
{
    public Task<IReadOnlyList<PrivacyPolicyVersion>> GetAllVersionsAsync(CancellationToken cancellationToken)
        => Task.FromResult(versions);
}
