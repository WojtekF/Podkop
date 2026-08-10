using Podkop.Statute.Domain;

namespace Podkop.Statute.Application;

public static class PrivacyPolicyVersionExtensions
{
    public static PrivacyPolicyDetail ToPrivacyPolicyDetail(this PrivacyPolicyVersion? policyVersion) =>
        policyVersion is not null
            ? new PrivacyPolicyDetail(
                policyVersion.Version,
                policyVersion.EffectiveFrom,
                policyVersion.Sections.Select(section =>
                    new PolicySectionDetail(section.Number, section.Title, section.Paragraphs)).ToList())
            : null;
}
