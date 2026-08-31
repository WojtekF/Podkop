using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The write half of the transactional outbox (issue #94, ADR 0014): a slice's announcements
///     become rows of the very save that makes the state change durable, which is the guarantee
///     the old publish-after-save arrangement could not give. The specs run against real
///     PostgreSQL because atomicity is the whole claim — the row and the comment either both land
///     or neither does, and only the real engine can be made to fail a commit and prove it.
///     Announcements are asserted by reading the table back, never through a publisher: nothing
///     is published at this stage, and the processor that eventually does is a separate concern.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class OutboxWriteTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000101");

    /// <summary>Pinned rather than inherited from the test run, so the stamp is falsifiable.</summary>
    private static readonly DateTimeOffset Now = At("2026-08-28T09:15:00Z");

    private readonly FakeTimeProvider _clock = new(Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>A comment the way the use case creates one — through the factory, so it raised its event.</summary>
    private static Comment PostedComment(Guid id)
    {
        var result = Comment.Post(
            id, FindingId, null, "grace_hopper", "A take worth judging.", At("2026-07-08T10:00:00Z"));
        Assert.Equal(PostCommentOutcome.Posted, result.Outcome);
        return result.Comment!;
    }

    /// <summary>A comment the way rehydration creates one — through the constructor, so it raised nothing.</summary>
    private static Comment RehydratedComment(Guid id) =>
        new(id, FindingId, null, "grace_hopper", "A take worth judging.", At("2026-07-08T10:00:00Z"));

    private async Task GivenComments(params Comment[] comments)
    {
        await using var context = database.CreateDbContext();
        context.Comments.AddRange(comments);
        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     One use case's worth of work through a context that carries the outbox interceptor —
    ///     the save is the seam under test, so the specs commit through the context itself rather
    ///     than through the unit of work, whose own publish-after-save path is a separate concern
    ///     until the cutover.
    /// </summary>
    private async Task InOneUseCase(Func<EfCommentRepository, FindingCommentsDbContext, Task> useCase)
    {
        await using var context = database.CreateDbContextWithOutbox(
            new FindingCommentsContractEventTranslator(), _clock);
        await useCase(new EfCommentRepository(context), context);
    }

    /// <summary>Everything the slice has announced, read back from its own schema.</summary>
    private async Task<IReadOnlyList<OutboxMessage>> AnnouncedAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt).ToListAsync();
    }

    [Fact]
    public async Task A_committed_post_announces_one_comment_posted_carrying_the_aggregates_facts()
    {
        await InOneUseCase(async (repository, context) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await context.SaveChangesAsync();
        });

        var row = Assert.Single(await AnnouncedAsync());

        // Loose on how the type is spelled, strict on what it must identify: the processor
        // resolves rows without knowing which slice wrote them, so a bare "CommentPosted" that
        // two slices could both claim is not enough.
        Assert.Contains(typeof(CommentPosted).FullName!, row.Type);

        // Case-insensitive on purpose — the round trip is the claim, not a casing convention.
        var posted = JsonSerializer.Deserialize<CommentPosted>(
            row.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(posted);
        Assert.Equal(CommentId, posted.CommentId);
        Assert.Equal(FindingId, posted.FindingId);
    }

    [Fact]
    public async Task An_announcement_is_stamped_from_the_supplied_clock_and_waits_to_be_published()
    {
        await InOneUseCase(async (repository, context) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await context.SaveChangesAsync();
        });

        var row = Assert.Single(await AnnouncedAsync());
        Assert.Equal(Now, row.OccurredAt);

        // The write side only ever leaves work for the processor; a row that arrives already
        // marked processed would never be published at all.
        Assert.Null(row.ProcessedAt);
    }

    [Fact]
    public async Task A_failed_commit_announces_nothing()
    {
        // The atomicity claim, and the reason the announcement is a row at all: the comment
        // cannot go in (its id is taken), so the announcement of it must not survive either.
        // An outbox written outside the failing transaction would leave this row behind.
        await GivenComments(RehydratedComment(CommentId));

        await InOneUseCase(async (repository, context) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
        });

        Assert.Empty(await AnnouncedAsync());
    }

    [Fact]
    public async Task An_add_alone_announces_nothing_the_commit_is_the_announcement()
    {
        // The one spec here that holds trivially while the interceptor is unimplemented — no
        // save happens, so nothing can go wrong yet. It is kept as a standing guard: the rows
        // belong to the commit, and an implementation that wrote them when the aggregate was
        // added would announce work that never became durable.
        await InOneUseCase(async (repository, _) =>
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None));

        Assert.Empty(await AnnouncedAsync());
    }

    [Fact]
    public async Task Committing_a_comment_that_raised_nothing_announces_nothing()
    {
        // Translation, not fabrication: the row exists because the aggregate raised something,
        // never merely because a comment was written.
        await InOneUseCase(async (repository, context) =>
        {
            await repository.AddAsync(RehydratedComment(CommentId), CancellationToken.None);
            await context.SaveChangesAsync();
        });

        Assert.Empty(await AnnouncedAsync());
    }

    [Fact]
    public async Task A_committed_vote_announces_nothing()
    {
        // Votes are the slice's own business — nothing outside hears about them, so a commit
        // that only records a vote must leave the outbox empty.
        await GivenComments(RehydratedComment(CommentId));

        await InOneUseCase(async (repository, context) =>
        {
            var comment = await repository.GetByIdAsync(CommentId, CancellationToken.None);
            Assert.Equal(ActionOutcome.Applied, comment!.SetVote("ada_lovelace", VoteDirection.Up));
            await context.SaveChangesAsync();
        });

        Assert.Empty(await AnnouncedAsync());
    }

    [Fact]
    public async Task A_second_commit_does_not_announce_the_post_again()
    {
        // A drained aggregate has nothing left to announce; otherwise every later save in the
        // same scope would duplicate the row and the processor would publish the post twice.
        await InOneUseCase(async (repository, context) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await context.SaveChangesAsync();
            await context.SaveChangesAsync();
        });

        Assert.Single(await AnnouncedAsync());
    }

    [Fact]
    public async Task A_failed_save_still_drains_the_aggregate_and_a_retried_save_announces_the_post_once()
    {
        // The drain belongs to the save, not to its success: attempting the save turned the
        // announcement into this context's own pending work, so the aggregate has nothing left
        // to say — and retrying the same work, once the obstruction is gone, must land the
        // comment together with its one announcement, not announce the post a second time.
        await GivenComments(RehydratedComment(CommentId));

        var posted = PostedComment(CommentId);

        await InOneUseCase(async (repository, context) =>
        {
            await repository.AddAsync(posted, CancellationToken.None);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());

            Assert.Empty(posted.DomainEvents);

            // The obstruction clears — the comment squatting on the id is deleted elsewhere —
            // and the same unit of work is retried.
            await using (var other = database.CreateDbContext())
            {
                other.Comments.Remove(await other.Comments.SingleAsync(c => c.Id == CommentId));
                await other.SaveChangesAsync();
            }

            await context.SaveChangesAsync();
        });

        Assert.Single(await AnnouncedAsync());
    }
}
