using Podkop.Tags.Contracts;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Application;

/// <summary>
///     The two edges where this slice's <see cref="TaggedContentType" /> meets a primitive: the
///     content-type strings the announce events carry inward (<see cref="TaggedContentTypes" />),
///     and the <c>type</c> field a tag-page item carries outward. Both live here with the DTOs
///     rather than in Domain: the index speaks the enum, and how a content type is spelled on a
///     wire is the application boundary's business.
/// </summary>
public static class TaggedContentTypeExtensions
{
    /// <summary>The wire spelling a tag-page item's <c>type</c> field carries.</summary>
    public static string ToApiString(this TaggedContentType contentType) => contentType switch
    {
        TaggedContentType.Finding => TaggedContentTypes.Finding,
        TaggedContentType.Entry => TaggedContentTypes.Entry,
        _ => throw new ArgumentOutOfRangeException(
            nameof(contentType), contentType, "Unknown tagged-content type."),
    };

    /// <summary>
    ///     The content type an announce event names, or <c>null</c> when it names one this slice
    ///     does not index. Null rather than a throw: a producer announcing a type the tag
    ///     namespace does not carry is a fact to be ignored, not a delivery to be failed forever.
    /// </summary>
    public static TaggedContentType? FromApiString(string? contentType) => contentType switch
    {
        TaggedContentTypes.Finding => TaggedContentType.Finding,
        TaggedContentTypes.Entry => TaggedContentType.Entry,
        _ => null,
    };

    /// <summary>
    ///     The content type a tag-page filter narrows to, or <c>null</c> for
    ///     <see cref="TagContentFilter.All" /> — the combined stream narrows to nothing.
    /// </summary>
    public static TaggedContentType? ToContentType(this TagContentFilter filter) => filter switch
    {
        TagContentFilter.All => null,
        TagContentFilter.Findings => TaggedContentType.Finding,
        TagContentFilter.Entries => TaggedContentType.Entry,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown tag-page filter."),
    };
}
