using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Podkop.FindingComments.Contracts;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The read half of the transactional outbox (issue #94, ADR 0014): what the write side
///     recorded as rows, the processor turns back into published contract events — oldest first,
///     a batch per pass, marking what it delivered and keeping what it could not, so that
///     delivery is at least once and a poison announcement neither loops forever nor dams the
///     queue. The processor is exercised against this slice's real outbox table because the
///     rows' round trip is the claim; the publisher is a recording stand-in, because what
///     consumers do with an event is their own slices' business.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class OutboxDeliveryTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    /// <summary>Pinned rather than inherited from the test run, so the stamp is falsifiable.</summary>
    private static readonly DateTimeOffset Now = At("2026-08-31T09:15:00Z");

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly CapturingContractEventPublisher _publisher = new();

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static CommentPosted Posted(string eventId, string commentId) =>
        new(Guid.Parse(eventId), Guid.Parse(commentId), FindingId);

    /// <summary>
    ///     A waiting announcement exactly as the write side records one: the contract event's
    ///     full name, its JSON, a creation-ordered id. The id's timestamp is what fixes the
    ///     row's place in the queue, so specs pass distinct instants to pin an order.
    /// </summary>
    private static OutboxMessage Announced(CommentPosted @event, string createdAt, string? occurredAt = null) =>
        new(
            Guid.CreateVersion7(At(createdAt)),
            typeof(CommentPosted).FullName!,
            JsonSerializer.Serialize(@event),
            At(occurredAt ?? createdAt));

    /// <summary>Each row lands in its own save, so insertion order is pinned, distinct from id order.</summary>
    private async Task GivenAnnounced(params OutboxMessage[] rows)
    {
        foreach (var row in rows)
        {
            await using var context = database.CreateDbContext();
            context.OutboxMessages.Add(row);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     One processor pass over a context of its own — the way each beat of the background
    ///     service resolves a fresh scope — against a registry holding what this system announces.
    /// </summary>
    private async Task OnePass(int batchSize = 20, int maxAttempts = 3)
    {
        await using var context = database.CreateDbContext();
        var processor = new OutboxProcessor(
            new ContractEventTypeRegistry([typeof(CommentPosted)]),
            _publisher,
            _clock,
            new OutboxProcessorOptions { BatchSize = batchSize, MaxAttempts = maxAttempts });
        await processor.ProcessPendingAsync(context, CancellationToken.None);
    }

    /// <summary>Every row in the outbox, in queue (creation) order.</summary>
    private async Task<IReadOnlyList<OutboxMessage>> RowsAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.OutboxMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
    }

    [Fact]
    public async Task A_waiting_announcement_is_published_as_the_event_it_was_and_marked_delivered()
    {
        var @event = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        await GivenAnnounced(Announced(@event, "2026-08-31T08:00:00Z"));

        await OnePass();

        // The whole event, identity included — a consumer downstream dedups by EventId, so a
        // delivery that dropped or reinvented it would defeat the inbox it feeds.
        Assert.Equal(@event, Assert.Single(_publisher.Published));

        var row = Assert.Single(await RowsAsync());
        Assert.Equal(Now, row.ProcessedAt);
        Assert.Equal(0, row.Attempts);
        Assert.Null(row.Error);
    }

    [Fact]
    public async Task A_delivered_announcement_is_never_published_again()
    {
        await GivenAnnounced(Announced(
            Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101"),
            "2026-08-31T08:00:00Z"));

        await OnePass();
        await OnePass();

        Assert.Single(_publisher.Published);
    }

    [Fact]
    public async Task Announcements_publish_oldest_first_by_creation_not_by_testimony_or_insertion()
    {
        var first = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        var second = Posted("e0000000-0000-4000-8000-000000000002", "c0000000-0000-4000-8000-000000000102");
        var third = Posted("e0000000-0000-4000-8000-000000000003", "c0000000-0000-4000-8000-000000000103");

        // Creation order is first→second→third (the ids say so); the recorded timestamps are
        // deliberately reversed and the insertion order deliberately shuffled, so ordering by
        // either testimony or arrival produces a different sequence and fails here.
        var firstRow = Announced(first, "2026-08-31T08:00:00Z", occurredAt: "2026-08-31T08:00:03Z");
        var secondRow = Announced(second, "2026-08-31T08:00:01Z", occurredAt: "2026-08-31T08:00:02Z");
        var thirdRow = Announced(third, "2026-08-31T08:00:02Z", occurredAt: "2026-08-31T08:00:01Z");
        await GivenAnnounced(thirdRow, firstRow, secondRow);

        await OnePass();

        Assert.Equal([first, second, third], _publisher.Published);
    }

    [Fact]
    public async Task One_pass_publishes_at_most_a_batch_leaving_the_rest_waiting()
    {
        var first = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        var second = Posted("e0000000-0000-4000-8000-000000000002", "c0000000-0000-4000-8000-000000000102");
        var third = Posted("e0000000-0000-4000-8000-000000000003", "c0000000-0000-4000-8000-000000000103");
        await GivenAnnounced(
            Announced(first, "2026-08-31T08:00:00Z"),
            Announced(second, "2026-08-31T08:00:01Z"),
            Announced(third, "2026-08-31T08:00:02Z"));

        await OnePass(batchSize: 2);
        Assert.Equal([first, second], _publisher.Published);

        await OnePass(batchSize: 2);
        Assert.Equal([first, second, third], _publisher.Published);
    }

    [Fact]
    public async Task A_failed_publish_leaves_the_announcement_waiting_with_the_failure_on_record()
    {
        var @event = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        await GivenAnnounced(Announced(@event, "2026-08-31T08:00:00Z"));
        _publisher.Poisoned.Add(@event.EventId);

        await OnePass();

        Assert.Empty(_publisher.Published);
        var row = Assert.Single(await RowsAsync());
        Assert.Null(row.ProcessedAt);
        Assert.Equal(1, row.Attempts);
        Assert.NotNull(row.Error);
    }

    [Fact]
    public async Task A_failure_does_not_dam_the_queue_later_announcements_still_publish()
    {
        var poison = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        var healthy = Posted("e0000000-0000-4000-8000-000000000002", "c0000000-0000-4000-8000-000000000102");
        await GivenAnnounced(
            Announced(poison, "2026-08-31T08:00:00Z"),
            Announced(healthy, "2026-08-31T08:00:01Z"));
        _publisher.Poisoned.Add(poison.EventId);

        await OnePass();

        // The younger announcement got through even though the older one ahead of it failed —
        // the accepted cost is that delivery order across rows is not guaranteed.
        Assert.Equal(healthy, Assert.Single(_publisher.Published));
        Assert.Equal(1, (await RowsAsync())[0].Attempts);
    }

    [Fact]
    public async Task A_retried_announcement_that_then_succeeds_is_delivered()
    {
        var @event = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        await GivenAnnounced(Announced(@event, "2026-08-31T08:00:00Z"));

        _publisher.Poisoned.Add(@event.EventId);
        await OnePass();

        // The consumer's trouble passes — the next beat delivers what the last one could not.
        _publisher.Poisoned.Clear();
        await OnePass();

        Assert.Equal(@event, Assert.Single(_publisher.Published));
        var row = Assert.Single(await RowsAsync());
        Assert.Equal(Now, row.ProcessedAt);
    }

    [Fact]
    public async Task After_the_cap_a_poison_announcement_is_parked_and_no_longer_tried()
    {
        var poison = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");
        await GivenAnnounced(Announced(poison, "2026-08-31T08:00:00Z"));
        _publisher.Poisoned.Add(poison.EventId);

        // One pass more than the cap allows: the fourth must not touch the row at all.
        await OnePass(maxAttempts: 3);
        await OnePass(maxAttempts: 3);
        await OnePass(maxAttempts: 3);
        await OnePass(maxAttempts: 3);

        Assert.Equal(3, _publisher.AttemptsAt(poison.EventId));
        var row = Assert.Single(await RowsAsync());
        Assert.Equal(3, row.Attempts);
        // Parked, not laundered: the row was never delivered and must not claim it was — an
        // operator recovers it by hand, guided by the recorded error.
        Assert.Null(row.ProcessedAt);
        Assert.NotNull(row.Error);
    }

    [Fact]
    public async Task An_announcement_naming_an_unheard_of_type_fails_like_any_other()
    {
        var ghost = new OutboxMessage(
            Guid.CreateVersion7(At("2026-08-31T08:00:00Z")),
            "Podkop.Ghost.Contracts.UnheardOf",
            "{}",
            At("2026-08-31T08:00:00Z"));
        var healthy = Posted("e0000000-0000-4000-8000-000000000002", "c0000000-0000-4000-8000-000000000102");
        await GivenAnnounced(ghost, Announced(healthy, "2026-08-31T08:00:01Z"));

        await OnePass();

        Assert.Equal(healthy, Assert.Single(_publisher.Published));
        var ghostRow = (await RowsAsync())[0];
        Assert.Null(ghostRow.ProcessedAt);
        Assert.Equal(1, ghostRow.Attempts);
        Assert.NotNull(ghostRow.Error);
    }

    [Fact]
    public async Task An_announcement_whose_payload_will_not_read_back_fails_like_any_other()
    {
        var garbled = new OutboxMessage(
            Guid.CreateVersion7(At("2026-08-31T08:00:00Z")),
            typeof(CommentPosted).FullName!,
            "this is not json",
            At("2026-08-31T08:00:00Z"));
        var healthy = Posted("e0000000-0000-4000-8000-000000000002", "c0000000-0000-4000-8000-000000000102");
        await GivenAnnounced(garbled, Announced(healthy, "2026-08-31T08:00:01Z"));

        await OnePass();

        Assert.Equal(healthy, Assert.Single(_publisher.Published));
        var garbledRow = (await RowsAsync())[0];
        Assert.Null(garbledRow.ProcessedAt);
        Assert.Equal(1, garbledRow.Attempts);
        Assert.NotNull(garbledRow.Error);
    }

    /// <summary>
    ///     Records what the processor hands over, in order, and can stand in for consumers that
    ///     are currently failing: publishing an announcement whose EventId is poisoned throws the
    ///     way a throwing handler would, and every handover — delivered or not — counts as an
    ///     attempt.
    /// </summary>
    private sealed class CapturingContractEventPublisher : IContractEventPublisher
    {
        private readonly List<object> _published = [];
        private readonly List<Guid> _attempts = [];

        public IReadOnlyList<object> Published => _published;

        public List<Guid> Poisoned { get; } = [];

        public int AttemptsAt(Guid eventId) => _attempts.Count(id => id == eventId);

        public Task PublishAsync(object contractEvent, CancellationToken cancellationToken)
        {
            if (contractEvent is CommentPosted posted)
            {
                _attempts.Add(posted.EventId);
                if (Poisoned.Contains(posted.EventId))
                    throw new InvalidOperationException("A consumer of this announcement is failing.");
            }

            _published.Add(contractEvent);
            return Task.CompletedTask;
        }
    }
}
