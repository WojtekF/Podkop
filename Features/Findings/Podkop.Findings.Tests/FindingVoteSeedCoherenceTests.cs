using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Testing;

namespace Podkop.Findings.Tests;

/// <summary>
///     The stub user must arrive with pre-existing finding votes so highlighting is visible on
///     first load (issue #15) — a pact the sample seed has kept since the in-memory store, and
///     which the database now has to carry (issue #67): the worker's own seed machinery puts the
///     generated findings into the slice's schema, and the app — production wiring, no overrides
///     — answers the same HTTP surface the frontend uses from what actually round-tripped
///     through PostgreSQL.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingVoteSeedCoherenceTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_stub_user_arrives_with_seeded_digs_and_at_least_one_bury_but_never_on_their_own_findings()
    {
        // The database is populated the way a fresh orchestrated volume is: the same seed step
        // the migration worker runs, over the same generator.
        await using (var context = database.CreateDbContext())
        {
            await FindingsSeed.SeedAsync(context, SampleFindings.Generate(), CancellationToken.None);
        }

        using var factory = new WebApplicationFactory<Program>()
            .WithPodkopDatabase(database.ConnectionString);
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
