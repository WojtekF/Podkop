namespace Podkop.Findings.Application;

/// <summary>
///     The Findings slice's ledger of contract events it has already acted on (issue #94, ADR
///     0014). Delivery through the outbox is at-least-once, so every consuming handler must be
///     able to recognize an announcement it has seen before — by the event's own identity, the
///     <c>EventId</c> its producer stamped — and act exactly once. Recording is tracked work like
///     any other mutation: it turns durable only through the slice's <see cref="IUnitOfWork" />,
///     in the same commit as the effect it guards, so the effect and the memory of having caused
///     it can never exist without each other.
/// </summary>
public interface IInbox
{
    /// <summary>Whether this slice has already acted on the announcement with this identity.</summary>
    Task<bool> AlreadyConsumedAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    ///     Tracks the announcement as acted on; durable once the use case's unit of work commits.
    /// </summary>
    Task RecordConsumedAsync(Guid eventId, CancellationToken cancellationToken);
}
