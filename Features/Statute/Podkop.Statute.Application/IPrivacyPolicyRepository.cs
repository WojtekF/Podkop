using Podkop.Statute.Domain;

namespace Podkop.Statute.Application;

public interface IPrivacyPolicyRepository
{
    Task<IReadOnlyList<PrivacyPolicyVersion>> GetAllVersionsAsync(CancellationToken cancellationToken);
}
