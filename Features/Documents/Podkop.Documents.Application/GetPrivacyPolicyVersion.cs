using MediatR;

namespace Podkop.Documents.Application;

/// <summary>
///     Query for one historical Privacy Policy version addressed by its version number (issue
///     #30). Yields <c>null</c> when no version carries that number, and also when the version
///     exists but is not yet in force: a published-but-future amendment stays hidden until its
///     effective-from instant, the same gate the current-document query applies. The endpoint
///     answers 404 for both.
/// </summary>
public sealed record GetPrivacyPolicyVersion(int Version) : IRequest<PrivacyPolicyDetail?>;

public sealed class GetPrivacyPolicyVersionHandler(
    IPrivacyPolicyRepository privacyPolicyRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetPrivacyPolicyVersion, PrivacyPolicyDetail?>
{
    public async Task<PrivacyPolicyDetail?> Handle(GetPrivacyPolicyVersion request, CancellationToken cancellationToken)
    {
        var policies = await privacyPolicyRepository.GetAllVersionsAsync(cancellationToken);
        var policy = policies
            .Where(s => s.EffectiveFrom <= timeProvider.GetUtcNow())
            .FirstOrDefault(p => p.Version == request.Version);

        return policy.ToPrivacyPolicyDetail();
    }
}
