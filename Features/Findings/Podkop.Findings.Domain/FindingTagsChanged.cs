using Podkop.Shared.Domain;

namespace Podkop.Findings.Domain;

/// <summary>
///     Internal domain event (issue #77): this finding now carries this tag set. Raised on every
///     change to the set, and translated by the slice's infrastructure into the public
///     <c>TaggedContentAnnounced</c> the Tags slice indexes (ADR 0009/0011). It carries the whole
///     set rather than a delta, because that is what the announcement carries — one announcement
///     describes the finding's membership completely.
/// </summary>
public sealed record FindingTagsChanged(
    Guid FindingId,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt) : IDomainEvent;
