using MediatR;

namespace Podkop.Tags.Contracts;

/// <summary>
///     Public contract event (ADR 0003, ADR 0009): a piece of content carries this tag set as of
///     now. Published by every content slice that joins the tag namespace — on creation and again
///     on every edit of the tag set — and consumed by the Tags slice, which keeps it as membership
///     rows (ADR 0011). The announcement carries the <b>whole</b> tag set rather than a delta, so
///     one announcement is enough to describe the content's membership completely: a tag dropped
///     from an edited set is a tag the index must stop listing.
///     <para>
///         Facts only, all primitive: <paramref name="ContentType" /> is one of
///         <see cref="TaggedContentTypes" />, <paramref name="Tags" /> are already canonical (the
///         producer folded them through <see cref="Tag" />), and
///         <paramref name="CreatedAt" /> is when the content itself was created — never when the
///         announcement was made — because that is the fact the tag page orders by, and re-editing
///         a tag set must not jump the content to the top of the stream. Deliberately no score:
///         the index carries none, and Best sort waits for a score-propagation decision (ADR
///         0011). <paramref name="EventId" /> is the announcement's own identity, distinct from
///         the facts it announces: delivery through the outbox is at-least-once (ADR 0014), so a
///         consumer that hears the same announcement twice recognizes it by this id and acts once.
///     </para>
/// </summary>
public sealed record TaggedContentAnnounced(
    Guid EventId,
    string ContentType,
    Guid ContentId,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt) : INotification;
