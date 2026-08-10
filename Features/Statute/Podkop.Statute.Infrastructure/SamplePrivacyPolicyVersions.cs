using Podkop.Statute.Domain;

namespace Podkop.Statute.Infrastructure;

/// <summary>
///     Development seed for the Privacy Policy until PostgreSQL persistence lands: the actual
///     shipped content of the document (issue #30). At least one version must be in force today,
///     describing in prose what personal data the service processes, why, and the rights users
///     have over it (the GDPR story agreed in ADR 0007 belongs in its content).
/// </summary>
public static class SamplePrivacyPolicyVersions
{
    public static IReadOnlyList<PrivacyPolicyVersion> Generate() => throw new NotImplementedException();
}
