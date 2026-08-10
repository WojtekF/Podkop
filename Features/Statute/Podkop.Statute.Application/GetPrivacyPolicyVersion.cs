using MediatR;

namespace Podkop.Statute.Application;

/// <summary>
///     Query for one historical Privacy Policy version addressed by its version number (issue
///     #30). Yields <c>null</c> when no version carries that number so the endpoint can answer
///     404.
/// </summary>
public sealed record GetPrivacyPolicyVersion(int Version) : IRequest<PrivacyPolicyDetail?>;

public sealed class GetPrivacyPolicyVersionHandler(IPrivacyPolicyRepository privacyPolicyRepository)
    : IRequestHandler<GetPrivacyPolicyVersion, PrivacyPolicyDetail?>
{
    public Task<PrivacyPolicyDetail?> Handle(GetPrivacyPolicyVersion request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
