using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The number a finding card advertises must equal what its discussion actually contains
///     (issue #16): the seeded comment threads are the authority for comment counts. These
///     tests run against the default composition root — no repository overrides — so they
///     exercise the real sample seeds through the same HTTP surface the frontend uses. The
///     feed is the surface that advertises counts, so feed-visible findings are the ones held
///     to account.
/// </summary>
public class SampleSeedCoherenceTests
{
    [Fact]
    public async Task Every_sample_findings_comment_count_equals_its_seeded_discussion_replies_included()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var feed = await client.GetFromJsonAsync<FeedPageResponse>("/api/findings?feed=main&limit=100");
        Assert.NotNull(feed);
        Assert.NotEmpty(feed.Items);

        var totalReplies = 0;
        foreach (var finding in feed.Items)
        {
            var threads = await client.GetFromJsonAsync<List<CommentThreadResponse>>(
                $"/api/findings/{finding.Id}/comments");
            Assert.NotNull(threads);
            var commentsInDiscussion = threads.Count + threads.Sum(t => t.Replies.Count);
            Assert.Equal(finding.CommentCount, commentsInDiscussion);
            totalReplies += threads.Sum(t => t.Replies.Count);
        }

        // Realistic seeds hold conversations, not only top-level comments — without any
        // replies, "replies included" would never actually be exercised above.
        Assert.True(totalReplies > 0, "expected at least one seeded reply across the sample findings");
    }

    [Fact]
    public async Task The_stub_user_arrives_with_scattered_comment_votes_but_never_on_their_own_comments()
    {
        // Seeded comment votes are what makes highlighting visible on first load (issue #18):
        // somewhere across the sample discussions the stub user (ada_lovelace) must already
        // hold votes — and never on a comment she authored, since own comments can't be voted.
        using var factory = new WebApplicationFactory<Program>();
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