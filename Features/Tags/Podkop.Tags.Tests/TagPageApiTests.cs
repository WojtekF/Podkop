using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Shared.Testing;
using Podkop.Tags.Contracts;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Tests;

/// <summary>
///     The Tag Page through the HTTP seam (issue #77, ADR 0011): the specs put membership rows
///     into the real database and override no service, so whatever the production wiring resolves
///     is what answers. What comes back is an ordered page of typed references — never card data
///     — newest created-at first, paged by 1-based page number (ADR 0004), narrowable by content
///     type, and 404 for a tag that no content carries.
/// </summary>
[Collection(TagsDatabaseCollection.Name)]
public class TagPageApiTests(TagsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Content ids are spelled out rather than random so every ordering below is falsifiable:
    ///     each spec chooses whether id order agrees with created-at order, and insertion order
    ///     agrees with neither, so a page assembled in the wrong one reads differently.
    /// </summary>
    private static Guid Id(int index) => new($"00000000-0000-0000-0077-{index:D12}");

    private static TagMembership Membership(
        string tag, int contentIndex, string createdAt, TaggedContentType type = TaggedContentType.Finding) =>
        new(tag, type, Id(contentIndex), At(createdAt));

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);

    private async Task GivenMemberships(params TagMembership[] memberships)
    {
        await using var context = database.CreateDbContext();
        context.TagMemberships.AddRange(memberships);
        await context.SaveChangesAsync();
    }

    private static Guid[] Ids(TagPageResponse page) => page.Items.Select(item => item.Id).ToArray();

    [Fact]
    public async Task A_tag_page_returns_the_items_and_has_next_page_envelope()
    {
        await GivenMemberships(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/dotnet");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<TagPageResponse>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task An_item_is_a_typed_reference_and_nothing_else()
    {
        // ADR 0011: the index serves refs, not cards — no title, no score, no author travels
        // with them. The frontend hydrates through the owning slice's batch endpoint.
        await GivenMemberships(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");

        var item = Assert.Single(page!.Items);
        Assert.Equal(TaggedContentTypes.Finding, item.Type);
        Assert.Equal(Id(1), item.Id);
    }

    [Fact]
    public async Task A_tag_page_lists_only_the_content_carrying_that_tag()
    {
        await GivenMemberships(
            Membership("dotnet", 1, "2026-07-08T10:00:00Z"),
            Membership("angular", 2, "2026-07-08T11:00:00Z"),
            Membership("dotnet", 3, "2026-07-08T09:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");

        Assert.Equal([Id(1), Id(3)], Ids(page!));
    }

    [Fact]
    public async Task A_tag_page_orders_content_by_creation_time_newest_first()
    {
        // Newest is Wykop's default and the only sort that ships; Best waits for a
        // score-propagation decision (ADR 0011), and the index carries no score to sort by.
        // Ids here ascend as created-at descends, so an id-ordered page reads backwards and an
        // insertion-ordered one starts at Id(1).
        await GivenMemberships(
            Membership("dotnet", 1, "2026-07-08T12:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T10:00:00Z"),
            Membership("dotnet", 3, "2026-07-08T11:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");

        Assert.Equal([Id(1), Id(3), Id(2)], Ids(page!));
    }

    [Fact]
    public async Task One_piece_of_content_appears_once_however_many_tags_it_carries()
    {
        await GivenMemberships(
            Membership("dotnet", 1, "2026-07-08T10:00:00Z"),
            Membership("angular", 1, "2026-07-08T10:00:00Z"),
            Membership("webdev", 1, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");

        Assert.Equal([Id(1)], Ids(page!));
    }

    [Fact]
    public async Task The_combined_stream_interleaves_the_content_types_by_creation_time()
    {
        // The stream model is the combined one from day one (ADR 0009): entries do not queue up
        // behind findings, they sort among them. Only this spec's seed is synthetic — no
        // Microblog slice exists yet to announce a real entry.
        await GivenMemberships(
            Membership("dotnet", 1, "2026-07-08T12:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T11:00:00Z", TaggedContentType.Entry),
            Membership("dotnet", 3, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");

        Assert.Equal([Id(1), Id(2), Id(3)], Ids(page!));
        Assert.Equal(
            [TaggedContentTypes.Finding, TaggedContentTypes.Entry, TaggedContentTypes.Finding],
            page!.Items.Select(item => item.Type).ToArray());
    }

    [Fact]
    public async Task The_type_filter_narrows_the_stream_to_one_content_type()
    {
        await GivenMemberships(
            Membership("dotnet", 1, "2026-07-08T12:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T11:00:00Z", TaggedContentType.Entry),
            Membership("dotnet", 3, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var findings = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?type=findings");
        var entries = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?type=entries");

        Assert.Equal([Id(1), Id(3)], Ids(findings!));
        Assert.Equal([Id(2)], Ids(entries!));
    }

    [Fact]
    public async Task No_type_filter_means_the_combined_stream()
    {
        await GivenMemberships(
            Membership("dotnet", 1, "2026-07-08T12:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T11:00:00Z", TaggedContentType.Entry));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var implicitAll = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");
        var explicitAll = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?type=all");

        Assert.Equal([Id(1), Id(2)], Ids(implicitAll!));
        Assert.Equal(Ids(implicitAll!), Ids(explicitAll!));
    }

    [Fact]
    public async Task A_type_filter_carrying_nothing_is_an_empty_page_of_a_tag_that_exists()
    {
        // Entries stays empty until the Microblog slice lands, and that view is a legitimate
        // view of a real tag — not a missing one. This is what lets the filter ship in full
        // today and light up with no rework later.
        await GivenMemberships(Membership("dotnet", 1, "2026-07-08T12:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/dotnet?type=entries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<TagPageResponse>();
        Assert.Empty(page!.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task A_tag_page_defaults_to_the_first_page()
    {
        await GivenMemberships(FiveMemberships());
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var withoutPage = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?limit=2");
        var pageOne = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?limit=2&page=1");

        Assert.Equal([Id(5), Id(4)], Ids(withoutPage!));
        Assert.Equal(Ids(withoutPage!), Ids(pageOne!));
    }

    [Fact]
    public async Task Later_pages_continue_where_the_previous_one_stopped()
    {
        await GivenMemberships(FiveMemberships());
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var pageTwo = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?limit=2&page=2");
        var pageThree = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet?limit=2&page=3");

        Assert.Equal([Id(3), Id(2)], Ids(pageTwo!));
        Assert.True(pageTwo!.HasNextPage);
        Assert.Equal([Id(1)], Ids(pageThree!));
        Assert.False(pageThree!.HasNextPage);
    }

    [Fact]
    public async Task A_page_past_the_end_of_an_existing_tag_is_empty_rather_than_missing()
    {
        // ADR 0004: a stale deep link degrades gracefully. The tag still exists, so this is not
        // the 404 case.
        await GivenMemberships(FiveMemberships());
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/dotnet?limit=2&page=9");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<TagPageResponse>();
        Assert.Empty(page!.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task A_tag_page_defaults_to_twenty_five_items()
    {
        await GivenMemberships(
            [.. Enumerable.Range(1, 26).Select(i => Membership("dotnet", i, $"2026-07-08T{i % 24:00}:00:00Z"))]);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>("/api/tags/dotnet");

        Assert.Equal(25, page!.Items.Count);
        Assert.True(page.HasNextPage);
    }

    [Theory]
    [InlineData("DOTNET")]
    [InlineData("DotNet")]
    [InlineData("dot-net")]
    public async Task Any_spelling_of_the_name_lands_on_the_canonical_tags_page(string spelling)
    {
        // The route value folds through the Tag value type, so /tag/POLSKA is /tag/polska —
        // one page, not a redirect and not a second index (research doc, section 3).
        await GivenMemberships(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<TagPageResponse>($"/api/tags/{spelling}");

        Assert.Equal([Id(1)], Ids(page!));
    }

    [Fact]
    public async Task An_unknown_tag_is_not_found()
    {
        // Wykop 404s unknown tags; there is no empty tag page (research doc, section 4).
        await GivenMemberships(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/qwertyzxcvbnm");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_tag_whose_last_content_has_gone_is_not_found_again()
    {
        // A tag exists exactly as long as content carries it (ADR 0009): once the index has
        // shrunk to nothing, its page goes back to being a page that never existed.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/dotnet");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_name_that_folds_to_no_tag_at_all_is_not_found()
    {
        await GivenMemberships(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/---");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("potato")]
    public async Task A_tag_page_rejects_malformed_page_numbers(string page)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/tags/dotnet?page={page}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task A_tag_page_rejects_out_of_range_limits(int limit)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/tags/dotnet?limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_tag_page_rejects_a_content_type_it_does_not_serve()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags/dotnet?type=photos");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    ///     Five memberships whose ids ascend exactly as their creation times do, so paging reads
    ///     down from Id(5) — and a page assembled in insertion or id order reads the other way.
    /// </summary>
    private static TagMembership[] FiveMemberships() =>
    [
        .. Enumerable.Range(1, 5).Select(i => Membership("dotnet", i, $"2026-07-08T{i + 8:00}:00:00Z"))
    ];

    private sealed record TagPageResponse(List<TagPageItem> Items, bool HasNextPage);

    private sealed record TagPageItem(string Type, Guid Id);
}
