namespace Podkop.FindingComments.Domain;

/// <summary>
///     The two sides of a comment vote — Upvote and Downvote in the glossary (CONTEXT.md),
///     deliberately distinct words from a finding's Dig and Bury.
/// </summary>
public enum VoteDirection
{
    Up,
    Down
}

public static class VoteDirectionExtensions
{
    public static string ToDomainString(this VoteDirection voteDirection)
    {
        return voteDirection == VoteDirection.Up ? "up" : "down";
    }
}