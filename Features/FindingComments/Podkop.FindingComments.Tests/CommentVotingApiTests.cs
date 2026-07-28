using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     Voting on comments (issue #18) through the HTTP seam: PUT is an idempotent set-my-vote
///     covering fresh votes and one-click side switches, DELETE withdraws, the ruleset mirrors
///     finding votes minus reasons, and the discussion payload carries the current user's vote
///     so highlighting survives a reload. The current user is the composition root's stub —
///     ada_lovelace — so "own comment" means a comment she authored.
/// </summary>
public class CommentVotingApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(Guid id) => new(
        id: id,
        title: "A finding under discussion",
        description: "The finding the votes land under.",
        source: new Uri("https://blog.example.org/posts/42"),
        thumbnail: null,
        author: "grace_hopper",
        tags: ["angular"],
        createdAt: At("2026-07-08T03:30:00Z"),
        promotedAt: At("2026-07-08T09:30:00Z"),
        digCount: 10,
        buryCount: 1,
        commentCount: 0);

    private static Comment CreateComment(
        Guid id,
        int upvotes,
        int downvotes,
        string author = "grace_hopper",
        Guid? parentCommentId = null,
        VoteDirection? stubUsersVote = null,
        string createdAt = "2026-07-08T10:00:00Z")
        => new(id, FindingId, parentCommentId, author, "A comment worth judging.", At(createdAt),
            upvotes, downvotes,
            stubUsersVote is null
                ? null
                : new Dictionary<string, VoteDirection> { [StubUser] = stubUsersVote.Value });

    private static WebApplicationFactory<Program> CreateFactory(IReadOnlyList<Comment> comments)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IFindingRepository>(
                    new InMemoryFindingRepository([CreateFinding(FindingId)]));
                services.AddSingleton<ICommentRepository>(new InMemoryCommentRepository(comments));
            }));

    private static Task<HttpResponseMessage> PutVote(HttpClient client, Guid commentId, string direction)
        => client.PutAsJsonAsync($"/api/comments/{commentId}/my-vote", new { direction });

    [Fact]
    public async Task Upvoting_a_fresh_comment_records_it_and_returns_the_new_counts()
    {
        using var factory = CreateFactory([CreateComment(CommentId, upvotes: 5, downvotes: 2)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(6, 2, "up"), votes);
    }

    [Fact]
    public async Task Downvoting_a_fresh_comment_works_symmetrically()
    {
        using var factory = CreateFactory([CreateComment(CommentId, upvotes: 5, downvotes: 2)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "down");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(5, 3, "down"), votes);
    }

    [Fact]
    public async Task Setting_the_side_already_held_changes_nothing()
    {
        // The seeded counts already contain the stub user's upvote.
        using var factory = CreateFactory(
            [CreateComment(CommentId, upvotes: 5, downvotes: 2, stubUsersVote: VoteDirection.Up)]);
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
        using var factory = CreateFactory(
        [
            CreateComment(parentId, upvotes: 1, downvotes: 0),
            CreateComment(replyId, upvotes: 5, downvotes: 2, author: "linus_t",
                parentCommentId: parentId, stubUsersVote: VoteDirection.Up),
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
        using var factory = CreateFactory(
            [CreateComment(CommentId, upvotes: 5, downvotes: 2, stubUsersVote: VoteDirection.Up)]);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/comments/{CommentId}/my-vote");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<CommentVotesResponse>();
        Assert.Equal(new CommentVotesResponse(4, 2, null), votes);
    }

    [Fact]
    public async Task Voting_on_your_own_comment_is_a_400()
    {
        using var factory = CreateFactory(
            [CreateComment(CommentId, upvotes: 5, downvotes: 2, author: StubUser)]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Voting_on_an_unknown_comment_is_a_404()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await PutVote(client, CommentId, "up");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Withdrawing_from_an_unknown_comment_is_a_404()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/comments/{CommentId}/my-vote");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_recorded_vote_survives_into_the_next_read()
    {
        using var factory = CreateFactory([CreateComment(CommentId, upvotes: 5, downvotes: 2)]);
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
    public async Task The_discussion_carries_the_readers_existing_votes_on_threads_and_replies()
    {
        var votedUpThread = Guid.Parse("c0000000-0000-4000-8000-000000000021");
        var votedDownReply = Guid.Parse("c0000000-0000-4000-8000-000000000022");
        var freshThread = Guid.Parse("c0000000-0000-4000-8000-000000000023");
        using var factory = CreateFactory(
        [
            // Net scores keep the thread order deterministic: 8 before -1.
            CreateComment(votedUpThread, upvotes: 10, downvotes: 2, stubUsersVote: VoteDirection.Up),
            CreateComment(votedDownReply, upvotes: 1, downvotes: 1, author: "linus_t",
                parentCommentId: votedUpThread, stubUsersVote: VoteDirection.Down),
            CreateComment(freshThread, upvotes: 3, downvotes: 4, author: "margaret_h"),
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
}
