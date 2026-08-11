using Podkop.Documents.Domain;

namespace Podkop.Documents.Application;

public interface IPrivacyPolicyRepository
{
    Task<IReadOnlyList<PrivacyPolicyVersion>> GetAllVersionsAsync(CancellationToken cancellationToken);
}
