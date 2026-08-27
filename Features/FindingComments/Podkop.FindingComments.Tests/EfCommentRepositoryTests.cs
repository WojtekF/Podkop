using System.Globalization;
using MediatR;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The EF-backed repository's reads against the live database (issue #68): a comment
///     rehydrates whole — parent reference, author, text, timestamp, and every recorded vote,
///     because both counts and the reader's own highlighted vote are derived from them — the
///     finding lookup answers exactly that finding's discussion and nobody else's, and the id
///     lookup answers null for an unknown id. The vote round trip runs against real PostgreSQL on
///     purpose: a comment whose votes quietly failed to load would still answer every in-memory
///     spec correctly while counts and highlights collapsed in the running app. Durability is the
///     unit of work's to prove — the commit round trips live in <see cref="EfUnitOfWorkTests" />.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class EfCommentRepositoryTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Comment CreateComment(
        Guid id,
        Guid? findingId = null,
        Guid? parentCommentId = null,
        string author = "grace_hopper",
        string text = "A take worth reading.",
        string createdAt = "2026-07-08T10:00:00Z",
        IReadOnlyDictionary<string, VoteDirection>? votes = null)
        => new(id, findingId ?? FindingId, parentCommentId, author, text, At(createdAt), votes);

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

    private async Task<IReadOnlyList<Comment>> Discussion(Guid findingId)
    {
        await using var context = database.CreateDbContext();
        return await new EfCommentRepository(context, new NoOpPublisher())
            .GetByFindingIdAsync(findingId, CancellationToken.None);
    }

    [Fact]
    public async Task A_comment_rehydrates_whole_from_the_database()
    {
        var parentId = Guid.Parse("c0000000-0000-4000-8000-000000000001");
        var replyId = Guid.Parse("c0000000-0000-4000-8000-000000000002");
        await GivenComments(
            CreateComment(parentId, text: "The parent take."),
            CreateComment(
                replyId,
                parentCommentId: parentId,
                author: "linus_t",
                text: "Agreed — with a caveat.",
                createdAt: "2026-07-08T11:00:00Z",
                votes: new Dictionary<string, VoteDirection>
                {
                    ["margaret_h"] = VoteDirection.Up,
                    ["dennis_r"] = VoteDirection.Down,
                    [StubUser] = VoteDirection.Up
                }));

        var reply = await LookedUp(replyId);

        Assert.NotNull(reply);
        Assert.Equal(FindingId, reply.FindingId);
        Assert.Equal(parentId, reply.ParentCommentId);
        Assert.True(reply.IsReply);
        Assert.Equal("linus_t", reply.Author);
        Assert.Equal("Agreed — with a caveat.", reply.Text);
        Assert.Equal(At("2026-07-08T11:00:00Z"), reply.CreatedAt);
        // Counts and the reader's highlight are derived from the rehydrated votes — a vote set
        // that quietly failed to load would zero all of these at once.
        Assert.Equal(2, reply.UpvoteCount);
        Assert.Equal(1, reply.DownvoteCount);
        Assert.Equal(VoteDirection.Up, reply.VoteBy(StubUser));
        Assert.Equal(VoteDirection.Down, reply.VoteBy("dennis_r"));
        Assert.Null(reply.VoteBy("nobody_who_voted"));
    }

    [Fact]
    public async Task A_top_level_comment_rehydrates_its_absent_parent()
    {
        var id = Guid.Parse("c0000000-0000-4000-8000-000000000003");
        await GivenComments(CreateComment(id));

        var comment = await LookedUp(id);

        Assert.NotNull(comment);
        Assert.Null(comment.ParentCommentId);
        Assert.False(comment.IsReply);
    }

    [Fact]
    public async Task The_lookup_answers_null_when_no_comment_carries_the_id()
    {
        await GivenComments(CreateComment(Guid.NewGuid()));

        Assert.Null(await LookedUp(Guid.Parse("c0000000-0000-4000-8000-00000000dead")));
    }

    [Fact]
    public async Task The_finding_lookup_answers_that_findings_whole_discussion_and_nothing_else()
    {
        var otherFinding = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");
        var topLevel = Guid.Parse("c0000000-0000-4000-8000-000000000011");
        var reply = Guid.Parse("c0000000-0000-4000-8000-000000000012");
        var strayId = Guid.Parse("c0000000-0000-4000-8000-000000000013");
        await GivenComments(
            CreateComment(topLevel),
            CreateComment(reply, parentCommentId: topLevel, author: "linus_t"),
            CreateComment(strayId, findingId: otherFinding, text: "Another finding's take."));

        var discussion = await Discussion(FindingId);

        // Top-level comments and replies alike — thread composition stays the query handler's
        // job, so the repository must answer the flat set, complete and uncontaminated.
        Assert.Equal(
            [topLevel, reply],
            discussion.Select(comment => comment.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task A_finding_without_discussion_answers_empty()
    {
        await GivenComments(CreateComment(Guid.NewGuid()));

        Assert.Empty(await Discussion(Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666")));
    }

    /// <summary>Adding publishes through the request's publisher; these read specs need none.</summary>
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
