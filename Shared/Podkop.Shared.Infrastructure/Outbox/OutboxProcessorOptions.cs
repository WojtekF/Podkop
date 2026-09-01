namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     How eagerly the outbox is drained (issue #94, ADR 0014). The poll interval is the bound on
///     the eventual-consistency window the ADR names; the batch size caps one pass's work; the
///     attempt cap is where a row that keeps failing stops being retried and stands parked with
///     its error on record. The values here are the defaults every host gets; a host or test
///     that needs a different cadence registers its own tuned instance.
/// </summary>
public sealed class OutboxProcessorOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 3;
}
