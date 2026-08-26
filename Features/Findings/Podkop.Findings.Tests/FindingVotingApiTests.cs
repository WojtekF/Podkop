using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Findings.Domain;
using Podkop.Shared.Testing;

namespace Podkop.Findings.Tests;

/// <summary>
///     Voting on findings (issue #15) through the HTTP seam, now against the durable store
///     (issue #67): PUT is an idempotent set-my-vote covering fresh digs and buries and one-click
///     side switches; a bury must name one of the five reasons; DELETE withdraws. The dig count
///     is the only public tally — the bury count and bury reasons appear in no response. The
///     current user is the composition root's stub — ada_lovelace — so "own finding" means one
///     she authored. The specs put findings into the real database and override no service, so
///     every request runs in its own scope over its own context: a vote that only changed an
///     in-memory aggregate — never saved — satisfies the mutation's response but is gone by the
///     next request, which is exactly what the cross-request specs here refuse to let pass.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingVotingApiTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(
        Guid id,
        int digCount,
        int buryCount,
        string author = "grace_hopper",
        FindingVote? stubUsersVote = null)
        => new(
            id: id,
            title: "A finding worth judging",
            description: "The finding the votes land on.",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: author,
            tags: ["angular"],
            createdAt: At("2026-07-08T03:30:00Z"),
            promotedAt: At("2026-07-08T09:30:00Z"),
            commentCount: 0,
            votes: SeedVotes(digCount, buryCount, stubUsersVote));

    /// <summary>
    ///     The crowd of untracked voters the seeded counts stand for, with the stub user's own vote
    ///     laid on top of them when she has one — never colliding, since the generated voters are
    ///     numbered rather than named.
    /// </summary>
    private static Dictionary<string, FindingVote> SeedVotes(int digCount, int buryCount, FindingVote? stubUsersVote)
    {
        var votes = VotesGenerator.Generate(digCount, buryCount);
        if (stubUsersVote is not null) votes[StubUser] = stubUsersVote;
        return votes;
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> PutDig(HttpClient client, Guid id)
        => client.PutAsJsonAsync($"/api/findings/{id}/my-vote", new { type = "dig" });

    private static Task<HttpResponseMessage> PutBury(HttpClient client, Guid id, string reason)
        => client.PutAsJsonAsync($"/api/findings/{id}/my-vote", new { type = "bury", reason });

    [Fact]
    public async Task Digging_a_fresh_finding_records_it_and_returns_the_new_dig_count()
    {
        await GivenFindings(CreateFinding(FindingId, 5, 1));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(6, "dig"), votes);
    }

    [Fact]
    public async Task Burying_a_fresh_finding_records_the_side_and_leaves_the_dig_count_alone()
    {
        await GivenFindings(CreateFinding(FindingId, 5, 1));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutBury(client, FindingId, "spam");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(5, "bury"), votes);
    }

    [Fact]
    public async Task Setting_the_side_already_held_changes_nothing()
    {
        // 4 other diggers plus the stub user's dig: the dig count already reads 5.
        await GivenFindings(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(5, "dig"), votes);
    }

    [Fact]
    public async Task Switching_from_dig_to_bury_moves_the_vote_in_one_request()
    {
        // Dig count reads 5 (4 others + the stub's dig); switching drops it back to 4.
        await GivenFindings(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutBury(client, FindingId, "duplicate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(4, "bury"), votes);
    }

    [Fact]
    public async Task Switching_from_bury_to_dig_moves_the_vote_in_one_request()
    {
        // Dig count reads 5 (the stub currently holds a bury, not a dig); switching lifts it to 6.
        await GivenFindings(
            CreateFinding(FindingId, 5, 1,
                stubUsersVote: new FindingVote(FindingVoteSide.Bury, BuryReason.Spam)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(6, "dig"), votes);
    }

    [Fact]
    public async Task Withdrawing_a_vote_frees_the_count_it_was_held_in()
    {
        await GivenFindings(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/findings/{FindingId}/my-vote");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(4, null), votes);
    }

    [Fact]
    public async Task Burying_without_a_reason_is_a_400()
    {
        await GivenFindings(CreateFinding(FindingId, 5, 1));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/findings/{FindingId}/my-vote", new { type = "bury" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Contains("reason", problem?.Detail);
    }

    [Fact]
    public async Task Voting_on_your_own_finding_is_a_400()
    {
        await GivenFindings(CreateFinding(FindingId, 5, 1, StubUser));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Contains("own finding", problem?.Detail);
    }

    [Fact]
    public async Task An_unrecognised_vote_type_is_a_400_that_names_the_valid_sides()
    {
        await GivenFindings(CreateFinding(FindingId, 5, 1));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/findings/{FindingId}/my-vote", new { type = "smash" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Contains("dig", problem?.Detail);
        Assert.Contains("bury", problem?.Detail);
    }

    [Fact]
    public async Task Voting_on_an_unknown_finding_is_a_404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Withdrawing_from_an_unknown_finding_is_a_404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/findings/{FindingId}/my-vote");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_recorded_vote_survives_into_the_next_detail_read()
    {
        // The detail read is a second request in its own scope over its own context: only a vote
        // the mutation actually made durable can still be there (issue #67).
        await GivenFindings(CreateFinding(FindingId, 5, 1));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var putResponse = await PutDig(client, FindingId);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        Assert.NotNull(detail);
        Assert.Equal(6, detail.DigCount);
        Assert.Equal("dig", detail.MyVote);
    }

    [Fact]
    public async Task A_withdrawn_vote_is_gone_by_the_next_detail_read()
    {
        // The withdrawal too must outlive its own request — a delete that only touched the
        // loaded aggregate would leave the highlight resurrected on reload.
        await GivenFindings(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var deleteResponse = await client.DeleteAsync($"/api/findings/{FindingId}/my-vote");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        Assert.NotNull(detail);
        Assert.Equal(4, detail.DigCount);
        Assert.Null(detail.MyVote);
    }

    [Fact]
    public async Task The_detail_carries_a_dig_the_reader_already_cast()
    {
        await GivenFindings(
            CreateFinding(FindingId, 5, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        Assert.NotNull(detail);
        Assert.Equal(6, detail.DigCount);
        Assert.Equal("dig", detail.MyVote);
    }

    [Fact]
    public async Task The_detail_carries_a_bury_the_reader_already_cast_without_exposing_its_reason()
    {
        await GivenFindings(
            CreateFinding(FindingId, 5, 1,
                stubUsersVote: new FindingVote(FindingVoteSide.Bury, BuryReason.InappropriateContent)));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{FindingId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        var detail = JsonSerializer.Deserialize<FindingDetailResponse>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(detail);
        Assert.Equal("bury", detail.MyVote);

        using var document = JsonDocument.Parse(json);
        var propertyNames = document.RootElement.EnumerateObject()
            .Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.DoesNotContain("buryreason", propertyNames);
        Assert.DoesNotContain("reason", propertyNames);
    }

    [Fact]
    public async Task A_mutation_response_exposes_the_dig_count_and_my_vote_but_never_a_bury_count()
    {
        await GivenFindings(CreateFinding(FindingId, 5, 1));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PutBury(client, FindingId, "spam");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var propertyNames = document.RootElement.EnumerateObject()
            .Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.Contains("digcount", propertyNames);
        Assert.Contains("myvote", propertyNames);
        Assert.DoesNotContain("burycount", propertyNames);
    }

    private sealed record FindingVotesResponse(int DigCount, string? MyVote);

    private sealed record ProblemResponse(string? Detail);

    private sealed record FindingDetailResponse(Guid Id, int DigCount, string? MyVote);
}
