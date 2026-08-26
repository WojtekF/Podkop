using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Findings.Domain;
using Podkop.Shared.Testing;

namespace Podkop.Findings.Tests;

/// <summary>
///     The finding detail through the HTTP seam, now against the durable store (issue #67): the
///     specs put findings into the real database and override no service, so the projection —
///     the full description, the derived domain, the public dig count and the never-public bury
///     count — answers from what actually round-tripped through PostgreSQL.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingDetailTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(
        Guid id,
        string title = "Angular 22 signals deep dive",
        string source = "https://blog.example.org/posts/42",
        string? thumbnail = "https://example.com/thumb.jpg",
        DateTimeOffset? promotedAt = null,
        int digCount = 123,
        int buryCount = 7,
        int commentCount = 9)
        => new(
            id: id,
            title: title,
            description: $"{title} — the full, untruncated description.",
            source: new Uri(source),
            thumbnail: thumbnail is null ? null : new Uri(thumbnail),
            author: "ada_lovelace",
            tags: ["angular", "webdev"],
            createdAt: (promotedAt ?? At("2026-07-08T09:30:00Z")).AddHours(-6),
            promotedAt: promotedAt ?? At("2026-07-08T09:30:00Z"),
            commentCount: commentCount,
            votes: VotesGenerator.Generate(digCount, buryCount));

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Detail_returns_the_finding_addressed_by_its_id()
    {
        var id = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
        var promotedAt = At("2026-07-08T09:30:00Z");
        await GivenFindings(CreateFinding(
            id,
            title: "Text-only finding",
            source: "https://blog.example.org/posts/42",
            thumbnail: null,
            promotedAt: promotedAt,
            digCount: 123,
            commentCount: 9));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<FindingDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal(id, detail.Id);
        Assert.Equal("Text-only finding", detail.Title);
        Assert.Equal("Text-only finding — the full, untruncated description.", detail.Description);
        Assert.Equal("https://blog.example.org/posts/42", detail.SourceUrl);
        Assert.Equal("blog.example.org", detail.Domain);
        Assert.Null(detail.ThumbnailUrl);
        Assert.Equal("ada_lovelace", detail.Author);
        Assert.Equal(["angular", "webdev"], detail.Tags);
        Assert.Equal(123, detail.DigCount);
        Assert.Equal(9, detail.CommentCount);
    }

    [Fact]
    public async Task Detail_exposes_the_thumbnail_when_the_finding_has_one()
    {
        var id = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");
        await GivenFindings(CreateFinding(id, thumbnail: "https://example.com/thumb.jpg"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{id}");

        Assert.NotNull(detail);
        Assert.Equal("https://example.com/thumb.jpg", detail.ThumbnailUrl);
    }

    [Fact]
    public async Task Detail_carries_both_the_created_and_promoted_timestamps()
    {
        var id = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");
        var promotedAt = At("2026-07-08T09:30:00Z");
        await GivenFindings(CreateFinding(id, promotedAt: promotedAt));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{id}");

        Assert.NotNull(detail);
        Assert.Equal(promotedAt.AddHours(-6), detail.CreatedAt);
        Assert.Equal(promotedAt, detail.PromotedAt);
    }

    [Fact]
    public async Task Detail_response_carries_no_bury_count()
    {
        var id = Guid.Parse("0d4f9a3e-4444-4222-8333-444455556666");
        await GivenFindings(CreateFinding(id, buryCount: 42));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var propertyNames = document.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();
        // The dig count is public and must be present; the bury count must not be exposed at all.
        Assert.Contains("digcount", propertyNames);
        Assert.DoesNotContain("burycount", propertyNames);
    }

    [Fact]
    public async Task Detail_of_an_unknown_id_is_a_404()
    {
        var known = Guid.Parse("0d4f9a3e-5555-4222-8333-444455556666");
        var unknown = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        await GivenFindings(CreateFinding(known));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{unknown}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record FindingDetailResponse(
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
