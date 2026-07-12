using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

public class MainPageFeedTests
{
    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(
        string title,
        DateTimeOffset? promotedAt,
        string source = "https://example.com/articles/1",
        string? thumbnail = "https://example.com/thumb.jpg",
        int digCount = 100,
        int commentCount = 10)
        => new(
            id: Guid.NewGuid(),
            title: title,
            description: $"{title} — description",
            source: new Uri(source),
            thumbnail: thumbnail is null ? null : new Uri(thumbnail),
            author: "grace_hopper",
            tags: ["dotnet", "webdev"],
            createdAt: (promotedAt ?? At("2026-07-01T00:00:00Z")).AddHours(-6),
            promotedAt: promotedAt,
            digCount: digCount,
            buryCount: 3,
            commentCount: commentCount);

    private static WebApplicationFactory<Program> CreateFactory(params Finding[] findings)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(findings))));

    [Fact]
    public async Task Main_feed_returns_the_items_and_cursor_envelope()
    {
        using var factory = CreateFactory(CreateFinding("Only finding", At("2026-07-08T10:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=main");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<FeedResponse>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Main_feed_contains_only_promoted_findings()
    {
        using var factory = CreateFactory(
            CreateFinding("Promoted A", At("2026-07-08T10:00:00Z")),
            CreateFinding("Still upcoming", promotedAt: null),
            CreateFinding("Promoted B", At("2026-07-08T11:00:00Z")),
            CreateFinding("Also upcoming", promotedAt: null));
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main");

        Assert.NotNull(page);
        Assert.Equal(["Promoted B", "Promoted A"], page.Items.Select(i => i.Title).ToArray());
    }

    [Fact]
    public async Task Main_feed_orders_findings_by_promotion_time_newest_first()
    {
        using var factory = CreateFactory(
            CreateFinding("Promoted at 10", At("2026-07-08T10:00:00Z")),
            CreateFinding("Promoted at 12", At("2026-07-08T12:00:00Z")),
            CreateFinding("Promoted at 11", At("2026-07-08T11:00:00Z")));
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main");

        Assert.NotNull(page);
        Assert.Equal(
            ["Promoted at 12", "Promoted at 11", "Promoted at 10"],
            page.Items.Select(i => i.Title).ToArray());
    }

    [Fact]
    public async Task Main_feed_caps_the_page_at_limit_and_returns_a_continuation_cursor()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main&limit=2");

        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
        Assert.NotNull(page.NextCursor);
    }

    [Fact]
    public async Task Main_feed_cursor_pages_through_the_whole_feed_without_gaps_or_repeats()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var seenIds = new List<Guid>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var url = cursor is null
                ? "/api/findings?feed=main&limit=2"
                : $"/api/findings?feed=main&limit=2&cursor={Uri.EscapeDataString(cursor)}";
            var page = await client.GetFromJsonAsync<FeedResponse>(url);

            Assert.NotNull(page);
            seenIds.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
            pages++;

            Assert.True(pages <= 5, "cursor never reached the end of the feed");
        } while (cursor is not null);

        Assert.Equal(3, pages);
        Assert.Equal(5, seenIds.Count);
        Assert.Equal(5, seenIds.Distinct().Count());
    }

    [Fact]
    public async Task Main_feed_is_empty_when_nothing_is_promoted_yet()
    {
        using var factory = CreateFactory(
            CreateFinding("Upcoming A", promotedAt: null),
            CreateFinding("Upcoming B", promotedAt: null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=main");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<FeedResponse>();
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Main_feed_card_exposes_source_url_and_derived_domain()
    {
        var promotedAt = At("2026-07-08T09:30:00Z");
        using var factory = CreateFactory(CreateFinding(
            "Text-only finding",
            promotedAt,
            source: "https://blog.example.org/posts/42",
            thumbnail: null,
            digCount: 123,
            commentCount: 7));
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main");

        Assert.NotNull(page);
        var item = Assert.Single(page.Items);
        Assert.Equal("Text-only finding", item.Title);
        Assert.Equal("https://blog.example.org/posts/42", item.SourceUrl);
        Assert.Equal("blog.example.org", item.Domain);
        Assert.Null(item.ThumbnailUrl);
        Assert.Equal("grace_hopper", item.Author);
        Assert.Equal(["dotnet", "webdev"], item.Tags);
        Assert.Equal(123, item.DigCount);
        Assert.Equal(7, item.CommentCount);
        Assert.Equal(promotedAt, item.PromotedAt);
    }

    [Fact]
    public async Task Main_feed_requires_the_feed_parameter()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Main_feed_rejects_unknown_feeds()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=upcoming");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Main_feed_rejects_out_of_range_limits(int limit)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings?feed=main&limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Main_feed_rejects_a_malformed_cursor()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=main&cursor=not-a-cursor");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Finding[] FivePromotedFindings() =>
        Enumerable.Range(1, 5)
            .Select(hour => CreateFinding($"Promoted {hour}", At($"2026-07-08T{hour:00}:00:00Z")))
            .ToArray();

    private sealed record FeedResponse(List<FeedItem> Items, string? NextCursor);

    private sealed record FeedItem(
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
        DateTimeOffset PromotedAt);
}
