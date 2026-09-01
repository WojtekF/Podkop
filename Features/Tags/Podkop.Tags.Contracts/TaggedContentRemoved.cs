using MediatR;

namespace Podkop.Tags.Contracts;

/// <summary>
///     Public contract event (ADR 0003, ADR 0009): a piece of content is gone, and with it every
///     tag it carried. The other direction of the announce pair (ADR 0011) — without it the index
///     could only ever grow, and a tag whose last content vanished would keep answering a page of
///     references to nothing instead of returning to 404.
///     <para>
///         Facts only, all primitive: <paramref name="ContentType" /> is one of
///         <see cref="TaggedContentTypes" />, and no tag set is carried — removal is unconditional,
///         so a consumer never has to have heard the announcement that put the content there.
///         <paramref name="EventId" /> is the announcement's own identity, for the same
///         at-least-once reason as on <see cref="TaggedContentAnnounced" />.
///     </para>
/// </summary>
public sealed record TaggedContentRemoved(
    Guid EventId,
    string ContentType,
    Guid ContentId) : INotification;
