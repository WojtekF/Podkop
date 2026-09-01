namespace Podkop.Tags.Domain;

/// <summary>
///     One row of the membership index (ADR 0011): this piece of content carries this tag, and
///     was created then. That is deliberately the whole of it — the index is a set of typed
///     references, never a copy of the content's card data, so a card's live facts (its score,
///     its comment count) can never go stale here because they were never here. The absence of a
///     score is what defers the Best sort until a score-propagation decision is taken; the
///     created-at is what Newest orders by.
///     <para>
///         The content reference is a plain (type, id) pair with no foreign key into any content
///         slice's schema (ADR 0010) — integrity stays at the contract-event level, which is the
///         only way the Tags slice may know anything about a finding or an entry at all. The tag
///         is already canonical: whatever put this row here folded it through
///         <c>Podkop.Tags.Contracts.Tag</c> first.
///     </para>
/// </summary>
public sealed class TagMembership
{
    public TagMembership(string tag, TaggedContentType contentType, Guid contentId, DateTimeOffset createdAt)
    {
        Tag = tag;
        ContentType = contentType;
        ContentId = contentId;
        CreatedAt = createdAt;
    }

    private TagMembership()
    {
    }

    /// <summary>The canonical tag name this row files its content under.</summary>
    public string Tag { get; } = string.Empty;

    public TaggedContentType ContentType { get; }

    public Guid ContentId { get; }

    /// <summary>When the content was created — the fact the tag page's Newest order runs on.</summary>
    public DateTimeOffset CreatedAt { get; }
}
