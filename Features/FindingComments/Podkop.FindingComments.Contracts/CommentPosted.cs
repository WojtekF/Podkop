using MediatR;

namespace Podkop.FindingComments.Contracts;

/// <summary>
///     Public contract event (ADR 0003): a comment was posted under a finding. Carries primitive
///     facts only — no domain types. <paramref name="EventId" /> is the announcement's own
///     identity, distinct from the facts it announces (issue #94): delivery through the outbox is
///     at-least-once, so a consumer that hears the same announcement twice recognizes it by this
///     id and acts once. The FindingComments slice stamps it when translating the internal
///     <c>CommentAdded</c> domain event; the Findings slice consumes the event to keep the
///     finding's comment count in sync (issue #17). The count is eventually consistent by design.
/// </summary>
public sealed record CommentPosted(Guid EventId, Guid CommentId, Guid FindingId) : INotification;
