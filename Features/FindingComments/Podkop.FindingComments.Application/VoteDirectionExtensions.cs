using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     The wire spelling of a comment-vote direction — the <c>"up"</c>/<c>"down"</c> strings the
///     <c>MyVote</c> DTO fields carry (issue #13's API contract). It lives here with those DTOs,
///     not in Domain: the aggregate speaks <see cref="VoteDirection" />, and how a direction is
///     spelled on the wire is the application boundary's business.
/// </summary>
public static class VoteDirectionExtensions
{
    public static string ToApiString(this VoteDirection direction) => direction switch
    {
        VoteDirection.Up => "up",
        VoteDirection.Down => "down",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown vote direction."),
    };

    public static string? ToApiString(this VoteDirection? direction) =>
        direction is null ? null : direction.Value.ToApiString();
}
