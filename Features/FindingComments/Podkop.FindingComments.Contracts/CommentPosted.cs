using MediatR;

namespace Podkop.FindingComments.Contracts;

/// <summary>
///     Public contract event (ADR 0003): a comment was posted under a finding. Carries primitive
///     facts only — no domain types. The FindingComments slice's infrastructure raises it after
///     persistence (translated from the internal <c>CommentAdded</c> domain event); the Findings
///     slice consumes it to keep the finding's comment count in sync (issue #17). The count is
///     eventually consistent by design.
/// </summary>
public sealed record CommentPosted(Guid CommentId, Guid FindingId) : INotification;
