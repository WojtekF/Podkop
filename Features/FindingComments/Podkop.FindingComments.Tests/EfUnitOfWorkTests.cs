using System.Globalization;
using MediatR;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The commit seam against the live database (issue #68, patterned on issue #96): a comment
///     the repository handed back — or one just added through it — turns durable only through the
///     unit of work's single explicit commit, and only then. A posted comment, a vote set or
///     withdrawn on a loaded comment, is then what the next context reads; a mutation never
///     committed is gone with its context.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class EfUnitOfWorkTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Comment CreateComment(
        Guid id,
        IReadOnlyDictionary<string, VoteDirection>? votes = null)
        => new(id, FindingId, null, "grace_hopper", "A take worth judging.", At("2026-07-08T10:00:00Z"), votes);

    private async Task GivenComments(params Comment[] comments)
    {
        await using var context = database.CreateDbContext();
        context.Comments.AddRange(comments);
        await context.SaveChangesAsync();
    }

    private async Task<Comment?> LookedUp(Guid id)
    {
        await using var context = database.CreateDbContext();
        return await new EfCommentRepository(context, new NoOpPublisher())
            .GetByIdAsync(id, CancellationToken.None);
    }

    /// <summary>
    ///     One use case's worth of work: repository and unit of work over the same context, the
    ///     way a handler's request scope shares one context between the two.
    /// </summary>
    private async Task InOneUseCase(Func<EfCommentRepository, Task> useCase, bool committed = true)
    {
        await using var context = database.CreateDbContext();
        await useCase(new EfCommentRepository(context, new NoOpPublisher()));
        if (committed) await new EfUnitOfWork(context).CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_committed_add_is_what_the_next_context_reads()
    {
        await InOneUseCase(repository =>
            repository.AddAsync(CreateComment(CommentId), CancellationToken.None));

        var reloaded = await LookedUp(CommentId);
        Assert.NotNull(reloaded);
        Assert.Equal("A take worth judging.", reloaded.Text);
    }

    [Fact]
    public async Task A_committed_vote_is_what_the_next_context_reads()
    {
        await GivenComments(CreateComment(CommentId, new Dictionary<string, VoteDirection>
        {
            ["linus_t"] = VoteDirection.Up
        }));

        await InOneUseCase(async repository =>
        {
            var comment = await repository.GetByIdAsync(CommentId, CancellationToken.None);
            Assert.Equal(ActionOutcome.Applied, comment!.SetVote(StubUser, VoteDirection.Up));
        });

        var reloaded = await LookedUp(CommentId);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.UpvoteCount);
        Assert.Equal(VoteDirection.Up, reloaded.VoteBy(StubUser));
    }

    [Fact]
    public async Task A_committed_side_switch_moves_the_vote_not_copies_it()
    {
        await GivenComments(CreateComment(CommentId, new Dictionary<string, VoteDirection>
        {
            [StubUser] = VoteDirection.Up
        }));

        await InOneUseCase(async repository =>
        {
            var comment = await repository.GetByIdAsync(CommentId, CancellationToken.None);
            comment!.SetVote(StubUser, VoteDirection.Down);
        });

        var reloaded = await LookedUp(CommentId);
        Assert.NotNull(reloaded);
        // The upvote is gone and the downvote stands in its place — a commit that appended
        // instead of moving would read one of each.
        Assert.Equal(0, reloaded.UpvoteCount);
        Assert.Equal(1, reloaded.DownvoteCount);
        Assert.Equal(VoteDirection.Down, reloaded.VoteBy(StubUser));
    }

    [Fact]
    public async Task A_committed_withdrawal_is_gone_for_the_next_context()
    {
        await GivenComments(CreateComment(CommentId, new Dictionary<string, VoteDirection>
        {
            ["linus_t"] = VoteDirection.Up,
            [StubUser] = VoteDirection.Up
        }));

        await InOneUseCase(async repository =>
        {
            var comment = await repository.GetByIdAsync(CommentId, CancellationToken.None);
            comment!.WithdrawVote(StubUser);
        });

        var reloaded = await LookedUp(CommentId);
        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded.UpvoteCount);
        Assert.Null(reloaded.VoteBy(StubUser));
    }

    [Fact]
    public async Task An_uncommitted_use_case_is_invisible_to_the_next_context()
    {
        // The other half of the seam's contract: nothing persists on its own — an add or a vote
        // that never commits has changed nothing.
        var addedId = Guid.Parse("c0000000-0000-4000-8000-000000000002");
        await GivenComments(CreateComment(CommentId));

        await InOneUseCase(async repository =>
        {
            await repository.AddAsync(CreateComment(addedId), CancellationToken.None);
            var comment = await repository.GetByIdAsync(CommentId, CancellationToken.None);
            comment!.SetVote(StubUser, VoteDirection.Up);
        }, committed: false);

        Assert.Null(await LookedUp(addedId));
        var reloaded = await LookedUp(CommentId);
        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.UpvoteCount);
        Assert.Null(reloaded.VoteBy(StubUser));
    }

    /// <summary>Adding publishes through the request's publisher; these round trips need none.</summary>
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;
    }
}
