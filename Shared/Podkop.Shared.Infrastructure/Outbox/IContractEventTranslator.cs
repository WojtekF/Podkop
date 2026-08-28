using Podkop.Shared.Domain;

namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     A slice's own answer to "which of my domain events does the outside world hear about, and
///     as what?" (ADR 0014). Translation happens at save time, in the slice that owns both sides
///     of it, so the outbox row already holds the public contract event and the processor that
///     publishes it needs to know nothing about any slice.
/// </summary>
public interface IContractEventTranslator
{
    /// <summary>
    ///     The public contract event announcing this domain event, or <c>null</c> when the event
    ///     is the slice's own business and nothing outside it should hear about it. Returns
    ///     <see cref="object" /> rather than a bus's message type so the shared outbox stays
    ///     independent of how announcements are eventually delivered.
    /// </summary>
    object? Translate(IDomainEvent domainEvent);
}
