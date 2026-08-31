namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     How the outbox processor hands a resolved announcement to whatever delivers it (issue #94,
///     ADR 0014). The shared outbox machinery deliberately knows no message bus — translators
///     hand it plain objects and it hands plain objects on — so the composition root decides what
///     publishing means (MediatR in-process today, a bus later, without the processor changing).
/// </summary>
public interface IContractEventPublisher
{
    /// <summary>Delivers one contract event to every consumer that listens for its type.</summary>
    Task PublishAsync(object contractEvent, CancellationToken cancellationToken);
}
