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
    private static DateTimeOffset At(string iso)
    {
        return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
    }

    private static Finding CreateFinding(
        string title,
        DateTimeOffset? promotedAt,
        string source = "https://example.com/articles/1",
        string? thumbnail = "https://example.com/thumb.jpg",
        int digCount = 100,
        int commentCount = 10,
        Guid? id = null)
    {
        return new Finding(
            id ?? Guid.NewGuid(),
            title,
            $"{title} — description",
            new Uri(source),
            thumbnail is null ? null : new Uri(thumbnail),
            "grace_hopper",
            ["dotnet", "webdev"],
            (promotedAt ?? At("2026-07-01T00:00:00Z")).AddHours(-6),
            promotedAt,
            commentCount,
            VotesGenerator.Generate(digCount, 0));
    }

    private static WebApplicationFactory<Program> CreateFactory(params Finding[] findings)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(findings))));
    }

    [Fact]
    public async Task Main_feed_returns_the_items_and_has_next_page_envelope()
    {
        using var factory = CreateFactory(CreateFinding("Only finding", At("2026-07-08T10:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=main");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<FeedResponse>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Main_feed_contains_only_promoted_findings()
    {
        using var factory = CreateFactory(
            CreateFinding("Promoted A", At("2026-07-08T10:00:00Z")),
            CreateFinding("Still upcoming", null),
            CreateFinding("Promoted B", At("2026-07-08T11:00:00Z")),
            CreateFinding("Also upcoming", null));
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
    public async Task Main_feed_defaults_to_the_first_page()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var withoutPage = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main&limit=2");
        var pageOne = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main&limit=2&page=1");

        Assert.NotNull(withoutPage);
        Assert.NotNull(pageOne);
        Assert.Equal(
            pageOne.Items.Select(i => i.Id).ToArray(),
            withoutPage.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task Main_feed_first_page_is_capped_at_limit_and_flags_a_next_page()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main&limit=2&page=1");

        Assert.NotNull(page);
        Assert.Equal(["Promoted 5", "Promoted 4"], page.Items.Select(i => i.Title).ToArray());
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task Main_feed_middle_page_continues_the_ordering_and_flags_a_next_page()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main&limit=2&page=2");

        Assert.NotNull(page);
        Assert.Equal(["Promoted 3", "Promoted 2"], page.Items.Select(i => i.Title).ToArray());
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task Main_feed_last_page_holds_the_remainder_and_flags_no_next_page()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main&limit=2&page=3");

        Assert.NotNull(page);
        Assert.Equal(["Promoted 1"], page.Items.Select(i => i.Title).ToArray());
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Main_feed_pages_through_the_whole_feed_without_gaps_or_repeats()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var seenIds = new List<Guid>();
        var page = 1;
        bool hasNextPage;

        do
        {
            var feedPage = await client.GetFromJsonAsync<FeedResponse>(
                $"/api/findings?feed=main&limit=2&page={page}");

            Assert.NotNull(feedPage);
            seenIds.AddRange(feedPage.Items.Select(i => i.Id));
            hasNextPage = feedPage.HasNextPage;
            page++;

            Assert.True(page <= 5, "paging never reached the end of the feed");
        } while (hasNextPage);

        Assert.Equal(4, page);
        Assert.Equal(5, seenIds.Count);
        Assert.Equal(5, seenIds.Distinct().Count());
    }

    [Fact]
    public async Task Main_feed_breaks_promotion_time_ties_by_id_descending()
    {
        // Findings promoted at the same instant need a deterministic secondary
        // order, or items could repeat or vanish across page boundaries.
        var promotedAt = At("2026-07-08T10:00:00Z");
        using var factory = CreateFactory(
            CreateFinding("Tied low id", promotedAt, id: Guid.Parse("00000000-0000-0000-0000-000000000001")),
            CreateFinding("Tied high id", promotedAt, id: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<FeedResponse>("/api/findings?feed=main");

        Assert.NotNull(page);
        Assert.Equal(["Tied high id", "Tied low id"], page.Items.Select(i => i.Title).ToArray());
    }

    [Fact]
    public async Task Main_feed_page_past_the_end_is_an_empty_ok_page()
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=main&limit=2&page=4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<FeedResponse>();
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Main_feed_is_empty_when_nothing_is_promoted_yet()
    {
        using var factory = CreateFactory(
            CreateFinding("Upcoming A", null),
            CreateFinding("Upcoming B", null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/findings?feed=main");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<FeedResponse>();
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Main_feed_card_exposes_source_url_and_derived_domain()
    {
        var promotedAt = At("2026-07-08T09:30:00Z");
        using var factory = CreateFactory(CreateFinding(
            "Text-only finding",
            promotedAt,
            "https://blog.example.org/posts/42",
            null,
            123,
            7));
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
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-page")]
    public async Task Main_feed_rejects_malformed_or_non_positive_pages(string page)
    {
        using var factory = CreateFactory(FivePromotedFindings());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings?feed=main&page={page}");

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

    private static Finding[] FivePromotedFindings()
    {
        return Enumerable.Range(1, 5)
            .Select(hour => CreateFinding($"Promoted {hour}", At($"2026-07-08T{hour:00}:00:00Z")))
            .ToArray();
    }

    private sealed record FeedResponse(List<FeedItem> Items, bool HasNextPage);

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
