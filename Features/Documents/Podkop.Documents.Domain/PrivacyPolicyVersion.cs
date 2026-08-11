namespace Podkop.Documents.Domain;

/// <summary>
///     One version of the Privacy Policy — the document describing what personal data the service
///     processes, why, and the rights users have over it (CONTEXT.md). Separate from the Statute
///     but versioned the same way: amendments ship as a new version, old versions remain readable
///     (issue #30). Its sections are prose, not citable points — Reports never cite it.
/// </summary>
public sealed class PrivacyPolicyVersion
{
    public PrivacyPolicyVersion(int version, DateTimeOffset effectiveFrom, IReadOnlyList<PolicySection> sections)
    {
        Version = version;
        EffectiveFrom = effectiveFrom;
        Sections = sections;
    }

    public int Version { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public IReadOnlyList<PolicySection> Sections { get; }
}
