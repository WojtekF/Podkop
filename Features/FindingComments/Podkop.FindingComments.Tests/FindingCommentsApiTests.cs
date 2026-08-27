using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.FindingComments.Tests;

public class FindingCommentsApiTests
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    private static DateTimeOffset At(string iso)
    {
        return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
    }

    private static Finding CreateFinding(Guid id)
    {
        return new Finding(
            id,
            "A finding under discussion",
            "The finding the threads hang off.",
            new Uri("https://blog.example.org/posts/42"),
            null,
            "grace_hopper",
            ["angular"],
            At("2026-07-08T03:30:00Z"),
            At("2026-07-08T09:30:00Z"),
            0);
    }

    private static Comment TopLevel(
        Guid id,
        int upvotes,
        int downvotes,
        string createdAt,
        string author = "ada_lovelace",
        string text = "A top-level comment.")
    {
        return new Comment(id, FindingId, null, author, text, At(createdAt),
            VotesGenerator.Generate(downvotes, upvotes));
    }

    private static Comment Reply(
        Guid id,
        Guid parentCommentId,
        string createdAt,
        string author = "linus_t",
        string text = "A reply.",
        int upvotes = 0,
        int downvotes = 0)
    {
        return new Comment(id, FindingId, parentCommentId, author, text, At(createdAt),
            VotesGenerator.Generate(downvotes, upvotes));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<Comment> comments)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IFindingRepository>(new StubFindingRepository(findings));
                services.AddSingleton<Podkop.Findings.Application.IUnitOfWork>(new StubUnitOfWork());
                services.AddSingleton(new InMemoryCommentStore(comments));
                services.AddScoped<ICommentRepository, InMemoryCommentRepository>();
            }));
    }

    [Fact]
    public async Task Comments_of_an_unknown_finding_are_a_404()
    {
        var unknown = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        using var factory = CreateFactory([CreateFinding(FindingId)], []);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{unknown}/comments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_finding_with_no_comments_has_an_empty_discussion_not_an_error()
    {
        using var factory = CreateFactory([CreateFinding(FindingId)], []);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{FindingId}/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var threads = await response.Content.ReadFromJsonAsync<List<CommentThreadResponse>>();
        Assert.NotNull(threads);
        Assert.Empty(threads);
    }

    [Fact]
    public async Task Top_level_comments_come_best_first_by_net_score()
    {
        // Net scores: middling 5-4=1, best 10-2=8, runnerUp 3-0=3. Ordering by raw upvotes
        // (10, 5, 3) or by age would each produce a different sequence, so only genuine
        // net-score ordering passes.
        var middling = Guid.Parse("c0000000-0000-4000-8000-000000000001");
        var best = Guid.Parse("c0000000-0000-4000-8000-000000000002");
        var runnerUp = Guid.Parse("c0000000-0000-4000-8000-000000000003");
        using var factory = CreateFactory(
            [CreateFinding(FindingId)],
            [
                TopLevel(middling, 5, 4, "2026-07-08T10:00:00Z"),
                TopLevel(best, 10, 2, "2026-07-08T11:00:00Z"),
                TopLevel(runnerUp, 3, 0, "2026-07-08T12:00:00Z")
            ]);
        using var client = factory.CreateClient();

        var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
            $"/api/findings/{FindingId}/comments");

        Assert.NotNull(threads);
        Assert.Equal([best, runnerUp, middling], threads.Select(t => t.Id));
    }

    [Fact]
    public async Task Tied_top_level_comments_come_oldest_first()
    {
        // The same net score reached through different vote mixes; the newer one is seeded
        // first so insertion order cannot masquerade as the tie-breaker.
        var newer = Guid.Parse("c0000000-0000-4000-8000-000000000004");
        var older = Guid.Parse("c0000000-0000-4000-8000-000000000005");
        using var factory = CreateFactory(
            [CreateFinding(FindingId)],
            [
                TopLevel(newer, 4, 1, "2026-07-08T12:00:00Z"),
                TopLevel(older, 3, 0, "2026-07-08T09:00:00Z")
            ]);
        using var client = factory.CreateClient();

        var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
            $"/api/findings/{FindingId}/comments");

        Assert.NotNull(threads);
        Assert.Equal([older, newer], threads.Select(t => t.Id));
    }

    [Fact]
    public async Task Replies_sit_under_their_parent_in_chronological_order()
    {
        var parentA = Guid.Parse("c0000000-0000-4000-8000-00000000000a");
        var parentB = Guid.Parse("c0000000-0000-4000-8000-00000000000b");
        var replyEarly = Guid.Parse("c0000000-0000-4000-8000-000000000011");
        var replyLate = Guid.Parse("c0000000-0000-4000-8000-000000000012");
        var replyToB = Guid.Parse("c0000000-0000-4000-8000-000000000013");
        using var factory = CreateFactory(
            [CreateFinding(FindingId)],
            [
                TopLevel(parentA, 1, 0, "2026-07-08T10:00:00Z"),
                // The late reply hugely outscores the early one — reply order must ignore votes.
                Reply(replyLate, parentA, "2026-07-08T15:00:00Z", upvotes: 50),
                TopLevel(parentB, 0, 0, "2026-07-08T11:00:00Z"),
                Reply(replyEarly, parentA, "2026-07-08T13:00:00Z"),
                Reply(replyToB, parentB, "2026-07-08T14:00:00Z")
            ]);
        using var client = factory.CreateClient();

        var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
            $"/api/findings/{FindingId}/comments");

        Assert.NotNull(threads);
        // Replies never surface as threads of their own.
        Assert.Equal([parentA, parentB], threads.Select(t => t.Id));
        Assert.Equal([replyEarly, replyLate], threads[0].Replies.Select(r => r.Id));
        Assert.Equal([replyToB], threads[1].Replies.Select(r => r.Id));
    }

    [Fact]
    public async Task A_comment_row_carries_author_text_both_vote_counts_and_when_it_was_written()
    {
        var topLevelId = Guid.Parse("c0000000-0000-4000-8000-000000000021");
        var replyId = Guid.Parse("c0000000-0000-4000-8000-000000000022");
        using var factory = CreateFactory(
            [CreateFinding(FindingId)],
            [
                TopLevel(topLevelId, 12, 2, "2026-07-08T10:00:00Z",
                    "grace_hopper", "Best take in the thread."),
                Reply(replyId, topLevelId, "2026-07-08T10:30:00Z",
                    "linus_t", "Agreed — with a caveat.", 1)
            ]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{FindingId}/comments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        var threads = JsonSerializer.Deserialize<List<CommentThreadResponse>>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(threads);
        var thread = Assert.Single(threads);
        Assert.Equal(topLevelId, thread.Id);
        Assert.Equal("grace_hopper", thread.Author);
        Assert.Equal("Best take in the thread.", thread.Text);
        Assert.Equal(12, thread.UpvoteCount);
        Assert.Equal(2, thread.DownvoteCount);
        Assert.Equal(At("2026-07-08T10:00:00Z"), thread.CreatedAt);

        var reply = Assert.Single(thread.Replies);
        Assert.Equal(replyId, reply.Id);
        Assert.Equal("linus_t", reply.Author);
        Assert.Equal("Agreed — with a caveat.", reply.Text);
        Assert.Equal(1, reply.UpvoteCount);
        Assert.Equal(0, reply.DownvoteCount);
        Assert.Equal(At("2026-07-08T10:30:00Z"), reply.CreatedAt);

        // The wire shape itself caps threads at one level: a reply carries no replies field.
        using var document = JsonDocument.Parse(json);
        var replyElement = document.RootElement[0].GetProperty("replies")[0];
        Assert.False(replyElement.TryGetProperty("replies", out _),
            "a reply must not carry replies — threads are exactly one level deep");
    }

    private sealed record CommentThreadResponse(
        Guid Id,
        string Author,
        string Text,
        int UpvoteCount,
        int DownvoteCount,
        DateTimeOffset CreatedAt,
        List<CommentReplyResponse> Replies);

    private sealed record CommentReplyResponse(
        Guid Id,
        string Author,
        string Text,
        int UpvoteCount,
        int DownvoteCount,
        DateTimeOffset CreatedAt);
}