namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     How eagerly the outbox is drained (issue #94, ADR 0014). The poll interval is the bound on
///     the eventual-consistency window the ADR names; the batch size caps one pass's work; the
///     attempt cap is where a row that keeps failing stops being retried and stands parked with
///     its error on record. Bound from configuration by the host so tests and operators tune the
///     cadence without recompiling.
/// </summary>
public sealed class OutboxProcessorOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 3;
}
