using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Contracts;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.Findings.Tests;

/// <summary>
///     What consuming <see cref="CommentPosted" /> means once the outbox owns delivery (issue
///     #94, ADR 0014): at-least-once, so the handler counts each announcement exactly once —
///     recognizing a redelivery by the announcement's own EventId through the slice's inbox, and
///     recording what it acted on in the same commit as the count itself. Each delivery runs in
///     a scope of its own over this slice's real schema, the way the processor's publisher
///     resolves a fresh handler per event. Referencing the producer's Contracts project is the
///     consumer's one sanctioned view of the FindingComments slice (ADR 0003).
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class CommentPostedConsumptionTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(Guid id, int commentCount) =>
        new(
            id: id,
            title: "A discussed finding",
            description: "A discussed finding — description",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: ["dotnet"],
            createdAt: At("2026-07-01T06:00:00Z"),
            promotedAt: At("2026-07-08T09:30:00Z"),
            commentCount: commentCount,
            votes: null);

    private static CommentPosted Posted(string eventId, string commentId) =>
        new(Guid.Parse(eventId), Guid.Parse(commentId), FindingId);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     One delivery's worth of work, wired the way the publisher's scope wires it: handler,
    ///     repository, unit of work, and inbox all over the same fresh context, so what this
    ///     delivery did — and remembered doing — is one commit.
    /// </summary>
    private async Task Delivered(CommentPosted @event)
    {
        await using var context = database.CreateDbContext();
        var handler = new CommentPostedHandler(
            new EfFindingRepository(context),
            new EfUnitOfWork(context),
            new EfInbox(context, TimeProvider.System));
        await handler.Handle(@event, CancellationToken.None);
    }

    private async Task<int> CommentCountOf(Guid findingId)
    {
        await using var context = database.CreateDbContext();
        var finding = await new EfFindingRepository(context).GetByIdAsync(findingId, CancellationToken.None);
        Assert.NotNull(finding);
        return finding.CommentCount;
    }

    /// <summary>The slice's memory of what it has acted on, read back from its own schema.</summary>
    private async Task<IReadOnlyList<InboxMessage>> ConsumedAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.InboxMessages.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task A_first_delivery_counts_the_comment_and_remembers_the_announcement()
    {
        await GivenFindings(CreateFinding(FindingId, commentCount: 7));
        var @event = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");

        await Delivered(@event);

        Assert.Equal(8, await CommentCountOf(FindingId));
        // The memory and the count commit together — an effect the slice cannot remember causing
        // would be repeated on redelivery.
        Assert.Equal(@event.EventId, Assert.Single(await ConsumedAsync()).Id);
    }

    [Fact]
    public async Task A_redelivered_announcement_is_not_counted_again()
    {
        // At-least-once delivery in its plainest form: the processor published, crashed before
        // marking the row, and published again — same EventId, same facts, two deliveries.
        await GivenFindings(CreateFinding(FindingId, commentCount: 7));
        var @event = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");

        await Delivered(@event);
        await Delivered(@event);

        Assert.Equal(8, await CommentCountOf(FindingId));
        Assert.Single(await ConsumedAsync());
    }

    [Fact]
    public async Task Distinct_announcements_each_count_their_own_comment()
    {
        // Holds trivially while the handler counts unconditionally; it stands as the boundary of
        // deduplication — swallowing genuine second announcements would be the opposite failure.
        await GivenFindings(CreateFinding(FindingId, commentCount: 7));

        await Delivered(Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101"));
        await Delivered(Posted("e0000000-0000-4000-8000-000000000002", "c0000000-0000-4000-8000-000000000102"));

        Assert.Equal(9, await CommentCountOf(FindingId));
    }

    [Fact]
    public async Task An_announcement_for_a_finding_that_no_longer_exists_is_consumed_without_effect()
    {
        // The finding is gone but the announcement was still heard: it is remembered as consumed
        // so a redelivery is never left waiting to count a comment on a finding that reappears —
        // and the delivery must succeed, or the processor would retry it to the cap for nothing.
        var @event = Posted("e0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000101");

        await Delivered(@event);

        Assert.Equal(@event.EventId, Assert.Single(await ConsumedAsync()).Id);
    }
}
