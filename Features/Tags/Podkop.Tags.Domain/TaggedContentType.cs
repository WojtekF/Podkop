namespace Podkop.Tags.Domain;

/// <summary>
///     A content type the tag namespace indexes, in this slice's own vocabulary. The announce
///     events speak primitives (<c>Podkop.Tags.Contracts.TaggedContentTypes</c>); the index
///     speaks this, so a row can never hold a content type nobody recognizes. Stored as its
///     readable name rather than a number (ADR 0010), so values stay legible in psql and survive
///     any future reordering.
/// </summary>
public enum TaggedContentType
{
    /// <summary>A Finding, announced by the Findings slice.</summary>
    Finding,

    /// <summary>A Microblog Entry, announced by the Microblog slice once it lands (issue #74).</summary>
    Entry
}
