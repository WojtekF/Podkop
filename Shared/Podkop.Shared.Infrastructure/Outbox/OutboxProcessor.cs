using Microsoft.EntityFrameworkCore;

namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     The read half of the transactional outbox (issue #94, ADR 0014): what the write side
///     recorded as rows, this turns back into published contract events — after the commit,
///     asynchronously, at least once. It knows no slice: the context it is handed decides whose
///     outbox is drained, the <see cref="ContractEventTypeRegistry" /> decides which stored names
///     it may resurrect, and the <see cref="IContractEventPublisher" /> decides what publishing
///     means.
///     <para>
///         What one pass must do: take up the waiting announcements — those never published and
///         not yet parked by <see cref="OutboxProcessorOptions.MaxAttempts" /> failures — oldest
///         first in creation order (ids are Guid v7, so id order is creation order; the recorded
///         timestamp is testimony, not the queue), and no more of them than
///         <see cref="OutboxProcessorOptions.BatchSize" /> allows. Each taken announcement is
///         resolved to the event it was and published; a delivered row is marked processed with
///         the moment <see cref="TimeProvider" /> reports, and a row that fails — whether its
///         name resolves to nothing, its payload will not read back, or a consumer throws — has
///         the failure recorded on it and the pass moves on to the rest: one poison announcement
///         must not dam the queue, which also means delivery order across rows is not guaranteed
///         and consumers must not assume it.
///     </para>
///     <para>
///         One instance of the system runs at a time today, so a pass assumes no rival is
///         draining the same outbox. Scaling out means claiming rows with
///         <c>FOR UPDATE SKIP LOCKED</c> before this assumption breaks; at-least-once semantics
///         tolerate the race meanwhile, but the double publishing it causes is waste.
///     </para>
///     Specified by <c>OutboxDeliveryTests</c> in the FindingComments slice.
/// </summary>
public sealed class OutboxProcessor(
    ContractEventTypeRegistry registry,
    IContractEventPublisher publisher,
    TimeProvider timeProvider,
    OutboxProcessorOptions options)
{
    /// <summary>
    ///     One pass over the given slice's outbox: publish what is waiting, mark what was
    ///     delivered, record what failed, and leave the rest for the next pass.
    /// </summary>
    public Task ProcessPendingAsync(DbContext sliceContext, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
