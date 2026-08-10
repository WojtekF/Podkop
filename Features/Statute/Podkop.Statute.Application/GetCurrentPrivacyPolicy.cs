using MediatR;

namespace Podkop.Statute.Application;

/// <summary>
///     Query for the Privacy Policy version currently in force — the one the public Privacy
///     Policy page renders (issue #30). The same in-force rule as the Statute's applies to its
///     effective-from dates. Yields <c>null</c> when no version is in force so the endpoint can
///     answer 404.
/// </summary>
public sealed record GetCurrentPrivacyPolicy : IRequest<PrivacyPolicyDetail?>;

public sealed record PrivacyPolicyDetail(
    int Version,
    DateTimeOffset EffectiveFrom,
    IReadOnlyList<PolicySectionDetail> Sections);

public sealed record PolicySectionDetail(
    int Number,
    string Title,
    IReadOnlyList<string> Paragraphs);

public sealed class GetCurrentPrivacyPolicyHandler(IPrivacyPolicyRepository privacyPolicyRepository)
    : IRequestHandler<GetCurrentPrivacyPolicy, PrivacyPolicyDetail?>
{
    public async Task<PrivacyPolicyDetail?> Handle(GetCurrentPrivacyPolicy request, CancellationToken cancellationToken)
    {
        var policies = await privacyPolicyRepository.GetAllVersionsAsync(cancellationToken);
        var latestPolicy = policies
            .Where(policy => policy.EffectiveFrom <= DateTimeOffset.UtcNow)
            .OrderByDescending(policy => policy.Version)
            .FirstOrDefault();

        return latestPolicy.ToPrivacyPolicyDetail();
    }
}
