using Podkop.Shared.Domain;

namespace Podkop.Findings.Domain;

/// <summary>
///     Internal domain event (issue #77): this finding is gone. Translated by the slice's
///     infrastructure into the public <c>TaggedContentRemoved</c> so the tag namespace stops
///     listing it (ADR 0011) — the direction that lets the index shrink, and lets a tag whose last
///     finding vanished return to 404.
/// </summary>
public sealed record FindingRemoved(Guid FindingId) : IDomainEvent;
