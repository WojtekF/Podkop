namespace Podkop.Findings.Domain;

/// <summary>
///     The two sides of a finding vote — Dig and Bury in the glossary (CONTEXT.md), deliberately
///     distinct words from a comment's Upvote and Downvote. A bury also carries a
///     <see cref="BuryReason" />; a dig does not.
/// </summary>
public enum FindingVoteSide
{
    Dig,
    Bury
}

public static class FindingVoteSideExtensions
{
    public static string ToApiString(this FindingVoteSide side)
    {
        return side == FindingVoteSide.Dig ? "dig" : "bury";
    }
}
