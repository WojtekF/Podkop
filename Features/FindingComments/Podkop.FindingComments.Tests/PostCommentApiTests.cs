using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.FindingComments.Domain;
using Podkop.Findings.Domain;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Shared.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     Posting comments and replies (issue #17) through the HTTP seam, now against the durable
///     store (issue #68): POST creates a top-level comment or a one-level-deep reply as the stub
///     user (ada_lovelace), text is trimmed and validated (empty and over-5000 rejected), the
///     depth invariant is enforced, and every error answer carries a stable
///     <c>podkop:problem:&lt;slug&gt;</c> ProblemDetails type so same-status outcomes stay
///     distinguishable. Posting also increments the finding's comment count via the CommentPosted
///     contract event — asserted across the slice boundary by re-fetching the finding, and since
///     the outbox owns delivery (issue #94, ADR 0014) that count is eventually consistent: the
///     count specs poll until the processor's next pass has counted the comment, within a window
///     bounded by the fast poll cadence the factory configures. The specs put the finding and any
///     pre-existing discussion into the real database and stub no service, so every request runs
///     in its own scope over its own contexts: a comment or a count that only ever changed in
///     memory — never committed — satisfies the posting response but is gone by the next request,
///     which is exactly what the read-back specs here refuse to let pass.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class PostCommentApiTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid TopLevelId = Guid.Parse("c0000000-0000-4000-8000-000000000001");
    private static readonly Guid ReplyId = Guid.Parse("c0000000-0000-4000-8000-000000000002");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso)
    {
        return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
    }

    private static Finding CreateFinding(Guid id, int commentCount = 0)
    {
        return new Finding(
            id: id,
            title: "A finding under discussion",
            description: "The finding the comments land under.",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: ["angular"],
            createdAt: At("2026-07-08T03:30:00Z"),
            promotedAt: At("2026-07-08T09:30:00Z"),
            commentCount: commentCount);
    }

    private static Comment CreateComment(Guid id, Guid? parentCommentId = null,
        string createdAt = "2026-07-08T10:00:00Z", Guid? findingId = null)
    {
        return new Comment(id, findingId ?? FindingId, parentCommentId, "grace_hopper", "An existing take.",
            At(createdAt));
    }

    /// <summary>
    ///     Seeds the world the specs post into — the finding in the Findings slice's schema, the
    ///     pre-existing discussion in this slice's — and answers a factory that stubs no service:
    ///     whatever the production wiring resolves is what handles the requests. The one thing it
    ///     tunes is the outbox delivery cadence, so the count specs' eventual-consistency window
    ///     is milliseconds here instead of the production poll interval.
    /// </summary>
    private async Task<WebApplicationFactory<Program>> GivenWorld(
        IReadOnlyList<Comment> comments, int seededCommentCount = 0)
    {
        await using (var findings = database.CreateFindingsDbContext())
        {
            findings.Findings.Add(CreateFinding(FindingId, seededCommentCount));
            await findings.SaveChangesAsync();
        }

        if (comments.Count > 0)
        {
            await using var context = database.CreateDbContext();
            context.Comments.AddRange(comments);
            await context.SaveChangesAsync();
        }

        return new WebApplicationFactory<Program>()
            .WithPodkopDatabase(database.ConnectionString)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton(new OutboxProcessorOptions
                {
                    PollInterval = TimeSpan.FromMilliseconds(50)
                })));
    }

    /// <summary>
    ///     The count is eventually consistent across the slice boundary (ADR 0014): the GET may
    ///     land before the processor's next pass, so the spec polls until the count arrives —
    ///     and on timeout asserts once more, so the failure names the count that never came.
    /// </summary>
    private static async Task AssertCommentCountEventuallyBecomes(HttpClient client, int expected)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        FindingResponse? finding = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            finding = await client.GetFromJsonAsync<FindingResponse>($"/api/findings/{FindingId}");
            if (finding!.CommentCount == expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(expected, finding?.CommentCount);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string? text, Guid? parentCommentId = null)
    {
        return client.PostAsJsonAsync($"/api/findings/{FindingId}/comments",
            new { text, parentCommentId });
    }

    [Fact]
    public async Task Posting_a_top_level_comment_returns_201_with_the_created_row()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, "A fresh take.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(StubUser, created.Author);
        Assert.Equal("A fresh take.", created.Text);
        // Nobody has voted on it yet, the poster included.
        Assert.Equal(0, created.UpvoteCount);
        Assert.Equal(0, created.DownvoteCount);
        Assert.Null(created.MyVote);
    }

    [Fact]
    public async Task Text_is_stored_trimmed()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, "  A fresh take. \n");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.Equal("A fresh take.", created!.Text);
    }

    [Fact]
    public async Task A_posted_comment_appears_in_the_next_read_of_the_discussion()
    {
        // The read is a second request in its own scope over its own context: only a comment the
        // posting request actually made durable can still be there (issue #68).
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, "A fresh take.");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var threads = await client.GetFromJsonAsync<List<ThreadResponse>>($"/api/findings/{FindingId}/comments");

        var thread = Assert.Single(threads!);
        Assert.Equal("A fresh take.", thread.Text);
        Assert.Equal(StubUser, thread.Author);
    }

    [Fact]
    public async Task Posting_a_reply_returns_201_and_lands_under_its_parent_chronologically()
    {
        using var factory = await GivenWorld([
            CreateComment(TopLevelId),
            CreateComment(ReplyId, parentCommentId: TopLevelId, createdAt: "2026-07-08T11:00:00Z"),
        ]);
        using var client = factory.CreateClient();

        var response = await Post(client, "An answer.", TopLevelId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var threads = await client.GetFromJsonAsync<List<ThreadResponse>>($"/api/findings/{FindingId}/comments");
        var thread = Assert.Single(threads!);
        // Replies are chronological, so the new one sits last.
        Assert.Equal(2, thread.Replies.Count);
        Assert.Equal("An answer.", thread.Replies[^1].Text);
        Assert.Equal(StubUser, thread.Replies[^1].Author);
    }

    [Fact]
    public async Task Posting_a_top_level_comment_increments_the_finding_comment_count()
    {
        // The count crosses the slice boundary as the CommentPosted contract event through the
        // outbox (issue #94): the posting commit records the announcement, the processor's next
        // pass delivers it, and the Findings slice's own commit counts it — so the count arrives
        // within the delivery window rather than inside the POST.
        using var factory = await GivenWorld([], seededCommentCount: 7);
        using var client = factory.CreateClient();

        var response = await Post(client, "A fresh take.");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await AssertCommentCountEventuallyBecomes(client, 8);
    }

    [Fact]
    public async Task Posting_a_reply_increments_the_finding_comment_count_too()
    {
        // Story 24: the count includes replies.
        using var factory = await GivenWorld([CreateComment(TopLevelId)], seededCommentCount: 1);
        using var client = factory.CreateClient();

        var response = await Post(client, "An answer.", TopLevelId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await AssertCommentCountEventuallyBecomes(client, 2);
    }

    [Fact]
    public async Task Empty_text_is_a_400_typed_comment_empty()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:comment-empty", problem?.Type);
    }

    [Fact]
    public async Task Whitespace_only_text_counts_as_empty()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, "  \n\t ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:comment-empty", problem?.Type);
    }

    [Fact]
    public async Task Text_over_5000_characters_is_a_400_typed_comment_too_long()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, new string('x', 5001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:comment-too-long", problem?.Type);
    }

    [Fact]
    public async Task Text_of_exactly_5000_characters_is_accepted()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, new string('x', 5000));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Replying_to_a_reply_is_a_400_typed_parent_is_a_reply()
    {
        using var factory = await GivenWorld([
            CreateComment(TopLevelId),
            CreateComment(ReplyId, parentCommentId: TopLevelId),
        ]);
        using var client = factory.CreateClient();

        var response = await Post(client, "Too deep.", ReplyId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:parent-is-a-reply", problem?.Type);
    }

    [Fact]
    public async Task Posting_under_an_unknown_finding_is_a_404_typed_unknown_finding()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/findings/{Guid.Parse("99999999-9999-4999-8999-999999999999")}/comments",
            new { text = "Into the void." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-finding", problem?.Type);
    }

    [Fact]
    public async Task Replying_to_an_unknown_parent_is_a_404_typed_unknown_parent()
    {
        using var factory = await GivenWorld([]);
        using var client = factory.CreateClient();

        var response = await Post(client, "An answer to nobody.",
            Guid.Parse("88888888-8888-4888-8888-888888888888"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-parent", problem?.Type);
    }

    [Fact]
    public async Task Replying_to_a_parent_from_another_finding_is_a_404_typed_unknown_parent()
    {
        // The parent exists and is top-level, but belongs to a different finding — for the
        // finding being posted under it is unknown, not a valid thread to land in.
        using var factory = await GivenWorld([
            CreateComment(TopLevelId, findingId: Guid.Parse("77777777-7777-4777-8777-777777777777")),
        ]);
        using var client = factory.CreateClient();

        var response = await Post(client, "An answer across findings.", TopLevelId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-parent", problem?.Type);
    }

    [Fact]
    public async Task A_rejected_post_leaves_the_discussion_and_the_count_untouched()
    {
        using var factory = await GivenWorld([], seededCommentCount: 3);
        using var client = factory.CreateClient();

        var response = await Post(client, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var threads = await client.GetFromJsonAsync<List<ThreadResponse>>($"/api/findings/{FindingId}/comments");
        Assert.Empty(threads!);
        var finding = await client.GetFromJsonAsync<FindingResponse>($"/api/findings/{FindingId}");
        Assert.Equal(3, finding!.CommentCount);
    }

    private sealed record CommentResponse(
        Guid Id,
        string Author,
        string Text,
        int UpvoteCount,
        int DownvoteCount,
        string? MyVote,
        DateTimeOffset CreatedAt);

    private sealed record ThreadResponse(
        Guid Id,
        string Author,
        string Text,
        List<CommentResponse> Replies);

    private sealed record FindingResponse(Guid Id, int CommentCount);

    private sealed record ProblemResponse(string? Type, string? Detail);
}
