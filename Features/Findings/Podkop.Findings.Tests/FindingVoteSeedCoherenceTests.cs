using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Podkop.Findings.Tests;

/// <summary>
///     The stub user must arrive with pre-existing finding votes so highlighting is visible on
///     first load (issue #15). These tests run against the default composition root — no
///     repository overrides — so they exercise the real sample seeds through the same HTTP
///     surface the frontend uses: the feed lists the findings, each finding's detail carries the
///     stub user's vote.
/// </summary>
public class FindingVoteSeedCoherenceTests
{
    [Fact]
    public async Task The_stub_user_arrives_with_seeded_digs_and_at_least_one_bury_but_never_on_their_own_findings()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var feed = await client.GetFromJsonAsync<FeedPageResponse>("/api/findings?feed=main&limit=100");
        Assert.NotNull(feed);
        Assert.NotEmpty(feed.Items);

        var details = new List<FindingDetailResponse>();
        foreach (var item in feed.Items)
        {
            var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{item.Id}");
            Assert.NotNull(detail);
            details.Add(detail);
        }

        // Some findings dug, at least one buried — otherwise nothing is highlighted on load.
        Assert.Contains(details, detail => detail.MyVote == "dig");
        Assert.Contains(details, detail => detail.MyVote == "bury");
        // ...and never on a finding she authored, since own findings can't be voted.
        Assert.All(details.Where(detail => detail.Author == "ada_lovelace"), detail => Assert.Null(detail.MyVote));
    }

    private sealed record FeedPageResponse(List<FeedFindingResponse> Items, bool HasNextPage);

    private sealed record FeedFindingResponse(Guid Id);

    private sealed record FindingDetailResponse(Guid Id, string Author, string? MyVote);
}
