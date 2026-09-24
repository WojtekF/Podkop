namespace Podkop.Tags.Contracts;

/// <summary>
///     The content types that may join the tag namespace, spelled as the primitives the announce
///     events carry (ADR 0009, ADR 0011). Contract events stay primitive-only, so the producing
///     and consuming slices agree on these strings rather than on a shared enum; each side maps
///     them to its own vocabulary at its own edge.
/// </summary>
public static class TaggedContentTypes
{
    /// <summary>A Finding, announced by the Findings slice.</summary>
    public const string Finding = "finding";

    /// <summary>A Microblog Entry, announced by the Microblog slice once it lands (issue #74).</summary>
    public const string Entry = "entry";
}
