using Podkop.Tags.Domain;

namespace Podkop.Tags.Application;

/// <summary>
///     The Tags slice's durable store seam for the membership index (issue #77, ADR 0010/0011),
///     read/track-only like every other slice's repository since issue #96: loaded rows are
///     change-tracked and additions are tracked, and nothing turns durable until the use case
///     commits through the slice's <see cref="IUnitOfWork" />.
/// </summary>
public interface ITagMembershipRepository
{
    /// <summary>
    ///     Whether any content at all carries this tag. The tag page's whole existence question
    ///     (ADR 0011): a tag exists exactly as long as content carries it, so this is what
    ///     separates a 404 from a page — and it deliberately ignores the type filter, because
    ///     narrowing an existing tag to a type that happens to carry nothing is an empty view of
    ///     a tag that exists, not a missing tag.
    /// </summary>
    Task<bool> AnyContentCarriesAsync(string tag, CancellationToken cancellationToken);

    /// <summary>
    ///     One page of a tag's stream, paged in the store rather than in memory: the rows
    ///     carrying <paramref name="tag" />, narrowed to <paramref name="contentType" /> when one
    ///     is given and spanning every type when it is not, in stream order — newest created-at
    ///     first, ties broken by content id descending — skipping the pages before
    ///     <paramref name="page" /> (1-based, ADR 0004) and answering up to
    ///     <paramref name="limit" /> + 1 rows, so the caller can tell a full last page from one
    ///     with a successor without a second query. A page past the end answers empty.
    /// </summary>
    Task<IReadOnlyList<TagMembership>> GetPageAsync(
        string tag,
        TaggedContentType? contentType,
        int page,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Every row currently filed for one piece of content, across all its tags.</summary>
    Task<IReadOnlyList<TagMembership>> GetForContentAsync(
        TaggedContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken);

    /// <summary>Tracks a new membership row; durable once the use case commits.</summary>
    void Add(TagMembership membership);

    /// <summary>Tracks the removal of membership rows; durable once the use case commits.</summary>
    void RemoveRange(IEnumerable<TagMembership> memberships);
}
