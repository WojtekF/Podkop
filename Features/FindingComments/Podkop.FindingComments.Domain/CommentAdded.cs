namespace Podkop.FindingComments.Domain;

/// <summary>
///     Domain event: a comment came into existence (issue #17). Internal to the slice (ADR
///     0003) — infrastructure translates it into the public <c>CommentPosted</c> contract event
///     after persistence; the domain event itself never leaves the slice.
/// </summary>
public sealed record CommentAdded(Guid CommentId, Guid FindingId) : IDomainEvent;
