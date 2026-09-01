using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Findings.Domain;
using Podkop.Shared.Testing;

namespace Podkop.Findings.Tests;

/// <summary>
///     The batch-by-ids endpoint through the HTTP seam (issue #77) — the obligation joining the
///     tag namespace puts on a content slice (ADR 0011): a tag page serves typed references, and
///     this is what turns the finding-shaped ones into cards, in one call per page. It answers the
///     same card data the feed serves, promoted and upcoming findings alike, and silently skips
///     ids naming nothing, because a reference whose content has just vanished is meant to
///     hydrate to nothing and be dropped.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingsBatchApiTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Guid Id(int index) => new($"00000000-0000-0000-0077-{index:D12}");

    private static Finding CreateFinding(int index, DateTimeOffset? promotedAt) =>
        new(
            id: Id(index),
            title: $"Finding {index}",
            description: $"Finding {index} — description",
            source: new Uri($"https://example.com/articles/{index}"),
            thumbnail: new Uri("https://example.com/thumb.jpg"),
            author: "grace_hopper",
            tags: ["dotnet", "webdev"],
            createdAt: At("2026-07-01T06:00:00Z"),
            promotedAt: promotedAt,
            commentCount: 10,
            votes: VotesGenerator.Generate(digCount: 100, buryCount: 3));

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    private static string Batch(params Guid[] ids) =>
        $"/api/findings/batch?ids={string.Join(',', ids)}";

    [Fact]
    public async Task The_batch_answers_the_findings_it_was_asked_for()
    {
        await GivenFindings(
            CreateFinding(1, At("2026-07-08T10:00:00Z")),
            CreateFinding(2, At("2026-07-08T11:00:00Z")),
            CreateFinding(3, At("2026-07-08T12:00:00Z")));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Batch(Id(1), Id(3)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = await response.Content.ReadFromJsonAsync<List<CardResponse>>();
        Assert.Equal([Id(1), Id(3)], cards!.Select(card => card.Id).Order().ToArray());
    }

    [Fact]
    public async Task A_hydrated_card_is_the_same_card_the_feed_serves()
    {
        await GivenFindings(CreateFinding(1, At("2026-07-08T10:00:00Z")));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Compared as JSON: the claim is that the two endpoints put the same fields with the
        // same values on the wire, which a record's own equality (reference-comparing its tag
        // list) would not actually check.
        var fromBatch = await client.GetFromJsonAsync<JsonElement>(Batch(Id(1)));
        var fromFeed = await client.GetFromJsonAsync<JsonElement>("/api/findings?feed=main");

        Assert.Equal(
            fromFeed.GetProperty("items")[0].GetRawText(),
            fromBatch[0].GetRawText());
    }

    [Fact]
    public async Task An_upcoming_finding_hydrates_too_and_carries_no_promotion_time()
    {
        // A tag page lists everything that took the tag, promoted or not — unlike the Main Page
        // feed. This is why the card's promotion time is optional and its creation time is not.
        await GivenFindings(CreateFinding(1, promotedAt: null));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var card = Assert.Single(await client.GetFromJsonAsync<List<CardResponse>>(Batch(Id(1)))!);

        Assert.Null(card.PromotedAt);
        Assert.Equal(At("2026-07-01T06:00:00Z"), card.CreatedAt);
    }

    [Fact]
    public async Task An_id_naming_nothing_is_simply_absent_rather_than_an_error()
    {
        // ADR 0011: a ref whose content has just vanished hydrates to nothing and the page drops
        // it — a briefly short page beats cross-slice consistency machinery.
        await GivenFindings(CreateFinding(1, At("2026-07-08T10:00:00Z")));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Batch(Id(1), Id(99)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = await response.Content.ReadFromJsonAsync<List<CardResponse>>();
        Assert.Equal([Id(1)], cards!.Select(card => card.Id).ToArray());
    }

    [Fact]
    public async Task A_batch_of_only_unknown_ids_is_an_empty_answer()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Batch(Id(99)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<CardResponse>>())!);
    }

    [Fact]
    public async Task The_batch_answers_one_card_per_id_however_often_an_id_is_named()
    {
        await GivenFindings(CreateFinding(1, At("2026-07-08T10:00:00Z")));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var cards = await client.GetFromJsonAsync<List<CardResponse>>(Batch(Id(1), Id(1)));

        Assert.Single(cards!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("potato")]
    public async Task The_batch_rejects_a_request_that_names_no_usable_ids(string ids)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/batch?ids={ids}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_batch_rejects_a_request_with_no_ids_at_all()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings/batch");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_batch_rejects_more_ids_than_one_page_could_ever_hold()
    {
        // Hydrating one page is what this serves, so the cap is the largest page a caller can
        // ask any feed for (ADR 0004).
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            Batch([.. Enumerable.Range(1, 101).Select(Id)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record CardResponse(
        Guid Id,
        string Title,
        string Description,
        string SourceUrl,
        string Domain,
        string? ThumbnailUrl,
        string Author,
        List<string> Tags,
        int DigCount,
        int CommentCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PromotedAt);
}
