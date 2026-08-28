using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     Turns what a slice's aggregates raised into outbox rows, as part of the very save that
///     makes their state durable (ADR 0014). This is the whole point of the pattern and the one
///     thing the loss window in the old publish-after-save arrangement could not offer: the rows
///     and the state change either both land or neither does, because they are one transaction.
///     <para>
///         What it must do, on a save that is about to happen: find the aggregates this context
///         is saving that have recorded something, ask the slice's
///         <see cref="IContractEventTranslator" /> what — if anything — the outside world should
///         hear about each recorded event, and record every answer it gets back as a row of this
///         save's own work. Events the slice keeps to itself produce no row. Each row must carry
///         the announcement in a form the processor can later turn back into the event it was,
///         without the processor knowing which slice wrote it, stamped with the moment it was
///         recorded as told by <see cref="TimeProvider" /> rather than by the wall clock, and
///         marked as still waiting to be published.
///     </para>
///     <para>
///         An aggregate that has been drained must not be drained again: committing a second time
///         announces nothing further, exactly as the aggregate raising nothing announces nothing.
///         The draining is generic — it knows only the shared kernel's aggregate and event
///         abstractions (ADR 0013), never any slice's vocabulary.
///     </para>
///     Specified by <c>OutboxWriteTests</c> in the FindingComments slice.
/// </summary>
public sealed class OutboxSaveChangesInterceptor(
    IContractEventTranslator translator,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
