namespace Podkop.Shared.Domain.Tests;

/// <summary>
///     Specifies the shared kernel's domain-event bookkeeping (issue #93) — the behavior the
///     Findings and FindingComments aggregates hand-rolled before the kernel existed, exercised
///     here through a test-local aggregate so the base class is pinned independently of any
///     slice's model.
/// </summary>
public class AggregateRootTests
{
    [Fact]
    public void A_new_aggregate_has_recorded_no_domain_events()
    {
        var aggregate = new TestAggregate();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Raising_an_event_exposes_it_through_DomainEvents()
    {
        var aggregate = new TestAggregate();
        var happened = new SomethingHappened("promoted");

        aggregate.RecordThat(happened);

        Assert.Same(happened, Assert.Single(aggregate.DomainEvents));
    }

    [Fact]
    public void Domain_events_are_exposed_in_the_order_they_were_raised()
    {
        var aggregate = new TestAggregate();
        var first = new SomethingHappened("first");
        var second = new SomethingElseHappened(2);
        var third = new SomethingHappened("third");

        aggregate.RecordThat(first);
        aggregate.RecordThat(second);
        aggregate.RecordThat(third);

        Assert.Equal<IDomainEvent>([first, second, third], aggregate.DomainEvents);
    }

    [Fact]
    public void The_same_event_raised_twice_is_recorded_twice()
    {
        var aggregate = new TestAggregate();
        var happened = new SomethingHappened("counted");

        aggregate.RecordThat(happened);
        aggregate.RecordThat(happened);

        Assert.Equal(2, aggregate.DomainEvents.Count);
    }

    [Fact]
    public void Clearing_drops_every_recorded_event()
    {
        var aggregate = new TestAggregate();
        aggregate.RecordThat(new SomethingHappened("published"));
        aggregate.RecordThat(new SomethingElseHappened(7));

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Clearing_an_aggregate_that_recorded_nothing_is_harmless()
    {
        var aggregate = new TestAggregate();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Events_raised_after_clearing_are_the_only_ones_exposed()
    {
        var aggregate = new TestAggregate();
        aggregate.RecordThat(new SomethingHappened("already published"));
        aggregate.ClearDomainEvents();
        var afterCommit = new SomethingHappened("raised since");

        aggregate.RecordThat(afterCommit);

        Assert.Same(afterCommit, Assert.Single(aggregate.DomainEvents));
    }

    [Fact]
    public void Each_aggregate_records_only_its_own_events()
    {
        var promoted = new TestAggregate();
        var untouched = new TestAggregate();

        promoted.RecordThat(new SomethingHappened("promoted"));

        Assert.Single(promoted.DomainEvents);
        Assert.Empty(untouched.DomainEvents);
    }

    [Fact]
    public void Clearing_one_aggregate_leaves_another_aggregates_events_intact()
    {
        var committed = new TestAggregate();
        var stillPending = new TestAggregate();
        committed.RecordThat(new SomethingHappened("published"));
        stillPending.RecordThat(new SomethingHappened("not yet published"));

        committed.ClearDomainEvents();

        Assert.Empty(committed.DomainEvents);
        Assert.Single(stillPending.DomainEvents);
    }

    /// <summary>
    ///     A stand-in for a slice's aggregate. <see cref="AggregateRoot.Raise" /> is the
    ///     aggregate's own to call, so the specs reach it through this type rather than from
    ///     outside.
    /// </summary>
    private sealed class TestAggregate : AggregateRoot
    {
        public void RecordThat(IDomainEvent domainEvent) => Raise(domainEvent);
    }

    private sealed record SomethingHappened(string What) : IDomainEvent;

    private sealed record SomethingElseHappened(int Which) : IDomainEvent;
}
