namespace Podkop.Shared.Domain;

/// <summary>
///     Base for aggregate roots that record what happened to them (issue #93). It owns the one
///     thing every aggregate in the codebase had hand-rolled: the list of domain events raised
///     since the aggregate was loaded or last drained, exposed read-only to the unit of work that
///     publishes and clears them after a successful commit.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    ///     The events raised on this aggregate and not yet cleared, oldest first. Read-only to
    ///     callers: only the aggregate itself adds, via <see cref="Raise" />.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    ///     Records that something happened to this aggregate. Visible only to the aggregate — an
    ///     aggregate's events are its own to raise.
    /// </summary>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    ///     Drops the recorded events, leaving the aggregate ready to record what happens next.
    ///     Called by the unit of work once the events have been published.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
