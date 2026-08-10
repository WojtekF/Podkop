using Podkop.Statute.Domain;

namespace Podkop.Statute.Infrastructure;

/// <summary>
///     Development seed for the Privacy Policy until PostgreSQL persistence lands: the actual
///     shipped content of the document (issue #30). One version is in force; its "Your rights"
///     section tells the erasure story agreed in ADR 0007 in prose.
/// </summary>
public static class SamplePrivacyPolicyVersions
{
    public static IReadOnlyList<PrivacyPolicyVersion> Generate() =>
    [
        new PrivacyPolicyVersion(
            1,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            [
                new PolicySection(1, "What this policy covers",
                [
                    "This policy describes what personal data Podkop processes, why it is processed, " +
                    "and the rights you have over it. It applies to every visitor and every account.",
                ]),
                new PolicySection(2, "Data we process",
                [
                    "The name your account appears under, together with everything you submit: " +
                    "findings, comments, dig and bury votes, comment votes, and reports.",
                    "Standard server logs — the network address and time of each request — kept " +
                    "briefly for security and troubleshooting.",
                ]),
                new PolicySection(3, "Why we process it",
                [
                    "To run the service: showing findings and their discussions, counting votes, and " +
                    "promoting findings to the Main Page.",
                    "To keep the community within the Statute: receiving reports, moderating content, " +
                    "and recording moderation actions.",
                ]),
                new PolicySection(4, "Your rights",
                [
                    "You may ask what data we hold about you and have inaccuracies corrected.",
                    "You may request the erasure of your account. Your findings and comments then " +
                    "remain available to the community, attributed to a neutral “Deleted " +
                    "Account” placeholder; your votes are kept without any link to you; your " +
                    "pending reports are discarded.",
                    "Until account management ships, requests are handled by the service operators.",
                ]),
            ]),
    ];
}
