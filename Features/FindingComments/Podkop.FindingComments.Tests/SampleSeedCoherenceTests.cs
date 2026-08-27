using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The number a finding card advertises must equal what its discussion actually contains
///     (issue #16): the seeded comment threads are the authority for comment counts. Since issue
///     #68 both sides of the pact live in PostgreSQL, each seeded by the migration worker's own
///     machinery — findings first, then the discussions hanging off them. These specs seed the
///     database the way the worker does and override no repository, so the two sides are held to
///     one story across the worker's two independent generator runs: every feed-visible
///     finding's count equals its discussion, and the discussions actually hang off the findings
///     the database holds.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class SampleSeedCoherenceTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<WebApplicationFactory<Program>> SeededAppAsync()
    {
        // The database is populated the way a fresh orchestrated volume is: the same seed steps
        // the migration worker runs, in the worker's order, over each slice's own generator —
        // the comments generator handed the same finding ids the findings seed persisted.
        var findings = SampleFindings.Generate();
        await using (var context = database.CreateFindingsDbContext())
        {
            await FindingsSeed.SeedAsync(context, findings, CancellationToken.None);
        }

        await using (var context = database.CreateDbContext())
        {
            await FindingCommentsSeed.SeedAsync(
                context,
                SampleFindingComments.GenerateFor([.. findings.Select(finding => finding.Id)]),
                CancellationToken.None);
        }

        return new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);
    }

    [Fact]
    public async Task Every_sample_findings_comment_count_equals_its_seeded_discussion_replies_included()
    {
        using var factory = await SeededAppAsync();
        using var client = factory.CreateClient();

        var feed = await client.GetFromJsonAsync<FeedPageResponse>("/api/findings?feed=main&limit=100");
        Assert.NotNull(feed);
        Assert.NotEmpty(feed.Items);

        var totalComments = 0;
        var totalReplies = 0;
        foreach (var finding in feed.Items)
        {
            var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
                $"/api/findings/{finding.Id}/comments");
            Assert.NotNull(threads);
            var commentsInDiscussion = threads.Count + threads.Sum(t => t.Replies.Count);
            Assert.Equal(finding.CommentCount, commentsInDiscussion);
            totalComments += commentsInDiscussion;
            totalReplies += threads.Sum(t => t.Replies.Count);
        }

        // The pact must be exercised for real, not satisfied by a world of zeroes: the seeded
        // discussions have to actually hang off the findings the database holds, and realistic
        // seeds hold conversations, not only top-level comments.
        Assert.True(totalComments > 0, "expected the seeded discussions to reference the seeded findings");
        Assert.True(totalReplies > 0, "expected at least one seeded reply across the sample findings");
    }

    [Fact]
    public async Task The_stub_user_arrives_with_scattered_comment_votes_but_never_on_their_own_comments()
    {
        // Seeded comment votes are what makes highlighting visible on first load (issue #18):
        // somewhere across the sample discussions the stub user (ada_lovelace) must already
        // hold votes — and never on a comment she authored, since own comments can't be voted.
        using var factory = await SeededAppAsync();
        using var client = factory.CreateClient();

        var feed = await client.GetFromJsonAsync<FeedPageResponse>("/api/findings?feed=main&limit=100");
        Assert.NotNull(feed);
        Assert.NotEmpty(feed.Items);

        var rows = new List<(string Author, string? MyVote)>();
        foreach (var finding in feed.Items)
        {
            var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
                $"/api/findings/{finding.Id}/comments");
            Assert.NotNull(threads);
            rows.AddRange(threads.Select(t => (t.Author, t.MyVote)));
            rows.AddRange(threads.SelectMany(t => t.Replies).Select(r => (r.Author, r.MyVote)));
        }

        Assert.Contains(rows, row => row.MyVote is not null);
        Assert.All(rows.Where(row => row.Author == "ada_lovelace"), row => Assert.Null(row.MyVote));
    }

    private sealed record FeedPageResponse(List<FeedFindingResponse> Items, bool HasNextPage);

    private sealed record FeedFindingResponse(Guid Id, int CommentCount);

    private sealed record CommentThreadResponse(
        Guid Id,
        string Author,
        string? MyVote,
        List<CommentReplyResponse> Replies);

    private sealed record CommentReplyResponse(Guid Id, string Author, string? MyVote);
}
