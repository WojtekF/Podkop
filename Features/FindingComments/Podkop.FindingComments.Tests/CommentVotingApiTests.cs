using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.FindingComments.Domain;
using Podkop.Findings.Domain;
using Podkop.Shared.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     Voting on comments (issue #18) through the HTTP seam, now against the durable store
///     (issue #68): PUT is an idempotent set-my-vote covering fresh votes and one-click side
///     switches, DELETE withdraws, the ruleset mirrors finding votes minus reasons, and the
///     discussion payload carries the current user's vote so highlighting survives a reload. The
///     current user is the composition root's stub — ada_lovelace — so "own comment" means a
///     comment she authored. The specs put the discussion into the real database and override no
///     service, so every request runs in its own scope over its own context: a vote that only
///     changed an in-memory comment — never committed — satisfies the mutation's response but is
///     gone by the next request, which is exactly what the cross-request spec here refuses to
///     let pass.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class CommentVotingApiTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso)
    {
        return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
    }

    private static Finding CreateFinding(Guid id)
    {
        return new Finding(
            id,
            "A finding under discussion",
            "The finding the votes land under.",
            new Uri("https://blog.example.org/posts/42"),
            null,
            "grace_hopper",
            ["angular"],
            At("2026-07-08T03:30:00Z"),
            At("2026-07-08T09:30:00Z"),
            0);
    }

    private static Comment CreateComment(
        Guid id,
        int upvotes,
        int downvotes,
        string author = "grace_hopper",
        Guid? parentCommentId = null,
        VoteDirection? stubUsersVote = null,
        string createdAt = "2026-07-08T10:00:00Z")
    {
        return new Comment(id, FindingId, parentCommentId, author, "A comment worth judging.", At(createdAt),
            stubUsersVote is null
                ? VotesGenerator.Generate(downvotes, upvotes)
                : new Dictionary<string, VoteDirection>(VotesGenerator.Generate(downvotes, upvotes))
                    { [StubUser] = stubUsersVote.Value });
    }

    /// <summary>
    ///     Seeds the finding into its slice's schema and the discussion into this slice's, then
    ///     answers a factory with no overrides: production wiring handles the requests.
    /// </summary>
    private async Task<WebApplicationFactory<Program>> GivenDiscussion(IReadOnlyList<Comment> comments)
    {
        await using (var findings = database.CreateFindingsDbContext())
        {
            findings.Findings.Add(CreateFinding(FindingId));
            await findings.SaveChangesAsync();
        }

        if (comments.Count > 0)
        {
            await using var context = database.CreateDbContext();
            context.Comments.AddRange(comments);
            await context.SaveChangesAsync();
        }

        return new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);
    }

    private static Task<HttpResponseMessage> PutVote(HttpClient client, Guid commentId, string direction)
    {
        return client.PutAsJsonAsync($"/api/comments/{commentId}/my-vote", new { direction });
    }

    [Fact]
    public async Task Upvoting_a_fresh_comment_records_it_and_returns_the_new_counts()
    {
        using var factory = await GivenDiscussion([CreateComment(CommentId, 5, 2)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(6, 2, "up"), votes);
    }

    [Fact]
    public async Task Downvoting_a_fresh_comment_works_symmetrically()
    {
        using var factory = await GivenDiscussion([CreateComment(CommentId, 5, 2)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "down");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(5, 3, "down"), votes);
    }

    [Fact]
    public async Task Setting_the_side_already_held_changes_nothing()
    {
        // The seeded counts does not contain the stub user's upvote.
        using var factory = await GivenDiscussion(
            [CreateComment(CommentId, 4, 2, stubUsersVote: VoteDirection.Up)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(5, 2, "up"), votes);
    }

    [Fact]
    public async Task Switching_sides_moves_the_vote_in_one_request_even_on_a_reply()
    {
        // 5/2 with the stub user's vote among the 5: a genuine switch lands on 4/3 — leaving
        // the old side (5/3) or double-counting (6/3) each produce a different pair.
        var parentId = Guid.Parse("c0000000-0000-4000-8000-00000000000a");
        var replyId = Guid.Parse("c0000000-0000-4000-8000-00000000000b");
        using var factory = await GivenDiscussion(
        [
            CreateComment(parentId, 1, 0),
            CreateComment(replyId, 4, 2, "linus_t",
                parentId, VoteDirection.Up)
        ]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, replyId, "down");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(4, 3, "down"), votes);
    }

    [Fact]
    public async Task Withdrawing_a_vote_frees_the_count_it_was_held_in()
    {
        using var factory = await GivenDiscussion(
            [CreateComment(CommentId, 4, 2, stubUsersVote: VoteDirection.Up)]);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/comments/{CommentId}/my-vote");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(4, 2, null), votes);
    }

    [Fact]
    public async Task Voting_on_your_own_comment_is_a_400()
    {
        using var factory = await GivenDiscussion(
            [CreateComment(CommentId, 5, 2, StubUser)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Contains("own comment", problem?.Detail);
    }

    [Fact]
    public async Task An_unrecognised_vote_direction_is_a_400_that_names_the_valid_directions()
    {
        using var factory = await GivenDiscussion([CreateComment(CommentId, 5, 2)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "sideways");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Contains("up", problem?.Detail);
        Assert.Contains("down", problem?.Detail);
    }

    [Fact]
    public async Task Voting_on_an_unknown_comment_is_a_404()
    {
        using var factory = await GivenDiscussion([]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Withdrawing_from_an_unknown_comment_is_a_404()
    {
        using var factory = await GivenDiscussion([]);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/comments/{CommentId}/my-vote");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_recorded_vote_survives_into_the_next_read()
    {
        // The read is a second request in its own scope over its own context: only a vote the
        // mutation actually made durable can still be there (issue #68).
        using var factory = await GivenDiscussion([CreateComment(CommentId, 5, 2)]);
        using var client = factory.CreateClient();

        var putResponse = await PutVote(client, CommentId, "up");
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
            $"/api/findings/{FindingId}/comments");

        Assert.NotNull(threads);
        var thread = Assert.Single(threads);
        Assert.Equal(6, thread.UpvoteCount);
        Assert.Equal(2, thread.DownvoteCount);
        Assert.Equal("up", thread.MyVote);
    }

    [Fact]
    public async Task A_withdrawn_vote_is_gone_by_the_next_read()
    {
        // The withdrawal too must outlive its own request — a delete that only touched the
        // loaded comment would leave the highlight resurrected on reload.
        using var factory = await GivenDiscussion(
            [CreateComment(CommentId, 4, 2, stubUsersVote: VoteDirection.Up)]);
        using var client = factory.CreateClient();

        var deleteResponse = await client.DeleteAsync($"/api/comments/{CommentId}/my-vote");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
            $"/api/findings/{FindingId}/comments");

        Assert.NotNull(threads);
        var thread = Assert.Single(threads);
        Assert.Equal(4, thread.UpvoteCount);
        Assert.Equal(2, thread.DownvoteCount);
        Assert.Null(thread.MyVote);
    }

    [Fact]
    public async Task The_discussion_carries_the_readers_existing_votes_on_threads_and_replies()
    {
        var votedUpThread = Guid.Parse("c0000000-0000-4000-8000-000000000021");
        var votedDownReply = Guid.Parse("c0000000-0000-4000-8000-000000000022");
        var freshThread = Guid.Parse("c0000000-0000-4000-8000-000000000023");
        using var factory = await GivenDiscussion(
        [
            // Net scores keep the thread order deterministic: 8 before -1.
            CreateComment(votedUpThread, 10, 2, stubUsersVote: VoteDirection.Up),
            CreateComment(votedDownReply, 1, 1, "linus_t",
                votedUpThread, VoteDirection.Down),
            CreateComment(freshThread, 3, 4, "margaret_h")
        ]);
        using var client = factory.CreateClient();

        var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
            $"/api/findings/{FindingId}/comments");

        Assert.NotNull(threads);
        Assert.Equal([votedUpThread, freshThread], threads.Select(t => t.Id));
        Assert.Equal("up", threads[0].MyVote);
        Assert.Null(threads[1].MyVote);
        var reply = Assert.Single(threads[0].Replies);
        Assert.Equal("down", reply.MyVote);
    }

    private sealed record CommentVotesResponse(int UpvoteCount, int DownvoteCount, string? MyVote);

    private sealed record CommentThreadResponse(
        Guid Id,
        int UpvoteCount,
        int DownvoteCount,
        string? MyVote,
        List<CommentReplyResponse> Replies);

    private sealed record CommentReplyResponse(Guid Id, string? MyVote);

    private sealed record ProblemResponse(string? Detail);
}
