using Podkop.Documents.Application;
using Podkop.Documents.Domain;

namespace Podkop.Documents.Infrastructure;

public sealed class InMemoryPrivacyPolicyRepository(IReadOnlyList<PrivacyPolicyVersion> versions)
    : IPrivacyPolicyRepository
{
    public Task<IReadOnlyList<PrivacyPolicyVersion>> GetAllVersionsAsync(CancellationToken cancellationToken)
        => Task.FromResult(versions);
}
