using Podkop.Tags.Application;
using Podkop.Tags.Contracts;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The membership rows the Development database is seeded with (issue #77). The index is
///     normally built only by consuming announce events, and the sample content is written
///     straight into its own slice's tables by the worker rather than announced — so the seed has
///     to stand in for the announcements that never happened, and must land the index in exactly
///     the state consuming them would have: every sample finding filed under every tag it
///     carries, each tag folded through <see cref="Tag" /> the way a real write would fold it,
///     and carrying the content's own created-at so the seeded tag pages come up in the same
///     Newest order a live one would.
///     <para>
///         Generation is deterministic, like every other sample generator: the same content in
///         yields the same rows out, in any process, so the seed coherence the tag pages depend on
///         survives being generated in the worker while the content it describes was generated
///         somewhere else.
///     </para>
/// </summary>
public static class SampleTagMemberships
{
    public static IReadOnlyList<TagMembership> GenerateFor(IReadOnlyList<SampleTaggedContent> content) =>
    [
        .. content.SelectMany(RowsFor)
    ];

    /// <summary>
    ///     The rows one piece of content contributes — exactly what consuming its announcement
    ///     would have filed. Content of a type the namespace does not carry files nothing, a tag
    ///     that folds to nothing files nothing, and two tags that fold to the same thing file one
    ///     row: (tag, type, id) is the row's identity, so a second copy would be a second row that
    ///     cannot exist.
    /// </summary>
    private static IEnumerable<TagMembership> RowsFor(SampleTaggedContent item)
    {
        var contentType = TaggedContentTypeExtensions.FromApiString(item.ContentType);
        if (contentType is null) return [];

        return item.Tags
            .Select(Tag.TryFold)
            .OfType<Tag>()
            .Select(tag => tag.Name)
            .Distinct()
            .Select(tag => new TagMembership(tag, contentType.Value, item.ContentId, item.CreatedAt));
    }
}
