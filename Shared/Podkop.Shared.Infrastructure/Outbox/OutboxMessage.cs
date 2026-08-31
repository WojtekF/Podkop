namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     One contract event a slice has announced, recorded as a row so the announcement commits in
///     the same transaction as the state change that caused it (ADR 0014). This is a persistence
///     record, not a domain object: it carries no feature vocabulary, and every converted slice
///     stores the identical shape in its own schema.
/// </summary>
public sealed class OutboxMessage
{
    public OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>
    ///     The contract event's type, named well enough that the processor can resolve it back to
    ///     a CLR type without knowing which slice wrote the row.
    /// </summary>
    public string Type { get; private set; } = null!;

    /// <summary>The serialized contract event — primitive-only by ADR 0003, so it keeps.</summary>
    public string Payload { get; private set; } = null!;

    /// <summary>When the announcement was recorded, taken from the writer's clock.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    ///     When the processor published this row, or <c>null</c> while it is still waiting. The
    ///     write side only ever leaves it null; marking rows processed belongs to the processor.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>
    ///     How many times the processor has tried to publish this row and failed (issue #94).
    ///     Zero for a row that has never been tried or that published on its first try; the
    ///     processor stops trying a row once its failures reach the configured cap, which is what
    ///     parks a poison row without ever pretending it was delivered.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    ///     What went wrong the last time publishing this row failed, or <c>null</c> if it never
    ///     has — the trace an operator reads before deciding a parked row's fate.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    ///     Records that the processor delivered this row at the given moment, after which no pass
    ///     may ever pick it up again.
    /// </summary>
    public void MarkProcessed(DateTimeOffset processedAt) => throw new NotImplementedException();

    /// <summary>
    ///     Records one failed delivery of this row: the failure joins the row's tally and its
    ///     description goes on record, while the row itself remains waiting — a failure is never
    ///     a delivery, so it must not retire the row the way <see cref="MarkProcessed" /> does.
    ///     Specified by <c>OutboxDeliveryTests</c> in the FindingComments slice.
    /// </summary>
    public void RecordFailure(string error) => throw new NotImplementedException();
}
