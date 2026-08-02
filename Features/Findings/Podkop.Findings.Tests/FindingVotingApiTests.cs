using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

// The finding factory below takes a run of same-typed ints, where the argument name is the only
// thing telling digs from buries. Code cleanup's positional argument style would strip exactly
// those names, so argument style is left to the call site in this file.
// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable ArgumentsStyleStringLiteral
// ReSharper disable ArgumentsStyleNamedExpression
// ReSharper disable ArgumentsStyleOther

namespace Podkop.Findings.Tests;

/// <summary>
///     Voting on findings (issue #15) through the HTTP seam: PUT is an idempotent set-my-vote
///     covering fresh digs and buries and one-click side switches; a bury must name one of the
///     five reasons; DELETE withdraws. The dig count is the only public tally — the bury count
///     and bury reasons appear in no response. The current user is the composition root's stub —
///     ada_lovelace — so "own finding" means one she authored. Seeded dig/bury counts are the
///     votes of other, untracked voters; the stub user's vote (when seeded) sits on top of them,
///     the same convention the comment-vote tests use.
/// </summary>
public class FindingVotingApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(
        Guid id,
        int digCount,
        int buryCount,
        string author = "grace_hopper",
        FindingVote? stubUsersVote = null)
        => new(
            id,
            "A finding worth judging",
            "The finding the votes land on.",
            new Uri("https://blog.example.org/posts/42"),
            null,
            author,
            ["angular"],
            At("2026-07-08T03:30:00Z"),
            At("2026-07-08T09:30:00Z"),
            0,
            SeedVotes(digCount, buryCount, stubUsersVote));

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

    private static WebApplicationFactory<Program> CreateFactory(params Finding[] findings)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(findings))));

    private static Task<HttpResponseMessage> PutDig(HttpClient client, Guid id)
        => client.PutAsJsonAsync($"/api/findings/{id}/my-vote", new { type = "dig" });

    private static Task<HttpResponseMessage> PutBury(HttpClient client, Guid id, string reason)
        => client.PutAsJsonAsync($"/api/findings/{id}/my-vote", new { type = "bury", reason });

    [Fact]
    public async Task Digging_a_fresh_finding_records_it_and_returns_the_new_dig_count()
    {
        using var factory = CreateFactory(CreateFinding(FindingId, 5, 1));
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(6, "dig"), votes);
    }

    [Fact]
    public async Task Burying_a_fresh_finding_records_the_side_and_leaves_the_dig_count_alone()
    {
        using var factory = CreateFactory(CreateFinding(FindingId, 5, 1));
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
        using var factory = CreateFactory(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
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
        using var factory = CreateFactory(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
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
        using var factory = CreateFactory(
            CreateFinding(FindingId, 5, 1,
                stubUsersVote: new FindingVote(FindingVoteSide.Bury, BuryReason.Spam)));
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(6, "dig"), votes);
    }

    [Fact]
    public async Task Withdrawing_a_vote_frees_the_count_it_was_held_in()
    {
        using var factory = CreateFactory(
            CreateFinding(FindingId, 4, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/findings/{FindingId}/my-vote");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var votes = await response.Content.ReadFromJsonAsync<FindingVotesResponse>();
        Assert.Equal(new FindingVotesResponse(4, null), votes);
    }

    [Fact]
    public async Task Burying_without_a_reason_is_a_400()
    {
        using var factory = CreateFactory(CreateFinding(FindingId, 5, 1));
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/findings/{FindingId}/my-vote", new { type = "bury" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Voting_on_your_own_finding_is_a_400()
    {
        using var factory = CreateFactory(CreateFinding(FindingId, 5, 1, StubUser));
        using var client = factory.CreateClient();

        var response = await PutDig(client, FindingId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        using var factory = CreateFactory(CreateFinding(FindingId, 5, 1));
        using var client = factory.CreateClient();

        var putResponse = await PutDig(client, FindingId);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        Assert.NotNull(detail);
        Assert.Equal(6, detail.DigCount);
        Assert.Equal("dig", detail.MyVote);
    }

    [Fact]
    public async Task The_detail_carries_a_dig_the_reader_already_cast()
    {
        using var factory = CreateFactory(
            CreateFinding(FindingId, 5, 1, stubUsersVote: new FindingVote(FindingVoteSide.Dig, null)));
        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        Assert.NotNull(detail);
        Assert.Equal(6, detail.DigCount);
        Assert.Equal("dig", detail.MyVote);
    }

    [Fact]
    public async Task The_detail_carries_a_bury_the_reader_already_cast_without_exposing_its_reason()
    {
        using var factory = CreateFactory(
            CreateFinding(FindingId, 5, 1,
                stubUsersVote: new FindingVote(FindingVoteSide.Bury, BuryReason.InappropriateContent)));
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
        using var factory = CreateFactory(CreateFinding(FindingId, 5, 1));
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

    private sealed record FindingDetailResponse(Guid Id, int DigCount, string? MyVote);
}
