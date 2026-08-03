using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

/// <summary>
///     The wire spelling of a finding-vote side — the <c>"dig"</c>/<c>"bury"</c> strings the
///     <c>MyVote</c> DTO fields carry (issue #15's API contract). It lives here with those DTOs,
///     not in Domain: the aggregate speaks <see cref="FindingVoteSide" />, and how a side is
///     spelled on the wire is the application boundary's business.
/// </summary>
public static class FindingVoteSideExtensions
{
    public static string? ToApiString(this FindingVoteSide? side) => side switch
    {
        FindingVoteSide.Dig => "dig",
        FindingVoteSide.Bury => "bury",
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown finding-vote side."),
    };
}
