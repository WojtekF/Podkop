namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     One announcement a consuming slice has already acted on, recorded so that hearing the same
///     announcement again — which at-least-once delivery guarantees will happen — changes nothing
///     (issue #94, ADR 0014). Keyed by the announcement's own identity (the contract event's
///     <c>EventId</c>), because that is the one fact that survives the trip from the producer's
///     outbox through the processor to every consumer. Like <see cref="OutboxMessage" />, this is
///     a persistence record with no feature vocabulary; every consuming slice stores the identical
///     shape in its own schema.
/// </summary>
public sealed class InboxMessage
{
    public InboxMessage(Guid id, DateTimeOffset consumedAt)
    {
        Id = id;
        ConsumedAt = consumedAt;
    }

    private InboxMessage()
    {
    }

    /// <summary>The consumed contract event's identity — its <c>EventId</c>, not an id of this row's own.</summary>
    public Guid Id { get; private set; }

    /// <summary>When this slice acted on the announcement, taken from the consumer's clock.</summary>
    public DateTimeOffset ConsumedAt { get; private set; }
}
