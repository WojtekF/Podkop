using System.Globalization;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

/// <summary>
///     The EF-backed repository against the live database (issue #67): the same
///     <see cref="IFindingRepository" /> contract the endpoint specs exercise over HTTP, now
///     proven where it can actually break — a finding rehydrates whole (votes, tags, counts and
///     timestamps included) and the feed page is composed by the database in feed order with the
///     one-past-the-limit next-page signal. The vote rehydration runs against real PostgreSQL on
///     purpose: an aggregate whose votes quietly fail to load would still answer every in-memory
///     spec correctly while dig counts and highlights collapsed in the running app. Durability is
///     no longer the repository's to prove (issue #96) — the commit round trips live in
///     <see cref="EfUnitOfWorkTests" />.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class EfFindingRepositoryTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(
        Guid id,
        string title,
        DateTimeOffset? promotedAt,
        string? thumbnail = "https://example.com/thumb.jpg",
        int commentCount = 0,
        IReadOnlyDictionary<string, FindingVote>? votes = null)
        => new(
            id: id,
            title: title,
            description: $"{title} — description",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: thumbnail is null ? null : new Uri(thumbnail),
            author: "grace_hopper",
            tags: ["dotnet", "webdev"],
            createdAt: At("2026-07-01T06:00:00Z"),
            promotedAt: promotedAt,
            commentCount: commentCount,
            votes: votes);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    private async Task<Finding?> LookedUp(Guid id)
    {
        await using var context = database.CreateDbContext();
        return await new EfFindingRepository(context).GetByIdAsync(id, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Finding>> PromotedPage(int page, int limit)
    {
        await using var context = database.CreateDbContext();
        return await new EfFindingRepository(context)
            .GetPromotedPageAsync(page, limit, CancellationToken.None);
    }

    [Fact]
    public async Task A_finding_rehydrates_whole_from_the_database()
    {
        var id = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
        await GivenFindings(CreateFinding(
            id,
            "Angular 22 signals deep dive",
            promotedAt: At("2026-07-08T09:30:00Z"),
            commentCount: 9,
            votes: new Dictionary<string, FindingVote>
            {
                ["linus_t"] = new(FindingVoteSide.Dig, null),
                ["margaret_h"] = new(FindingVoteSide.Dig, null),
                ["dennis_r"] = new(FindingVoteSide.Bury, BuryReason.Spam),
                [StubUser] = new(FindingVoteSide.Dig, null)
            }));

        var finding = await LookedUp(id);

        Assert.NotNull(finding);
        Assert.Equal("Angular 22 signals deep dive", finding.Title);
        Assert.Equal("Angular 22 signals deep dive — description", finding.Description);
        Assert.Equal("https://blog.example.org/posts/42", finding.Source.AbsoluteUri);
        Assert.Equal("https://example.com/thumb.jpg", finding.Thumbnail?.AbsoluteUri);
        Assert.Equal("grace_hopper", finding.Author);
        // The server's ordering is the frontend's ordering (ADR 0004 spirit): tags read back in
        // the order they were written, not alphabetized by an index or a join.
        Assert.Equal(["dotnet", "webdev"], finding.Tags);
        Assert.Equal(At("2026-07-01T06:00:00Z"), finding.CreatedAt);
        Assert.Equal(At("2026-07-08T09:30:00Z"), finding.PromotedAt);
        Assert.Equal(9, finding.CommentCount);
        // Counts and the reader's highlight are derived from the rehydrated votes — a vote set
        // that quietly failed to load would zero all three of these at once.
        Assert.Equal(3, finding.DigCount);
        Assert.Equal(1, finding.BuryCount);
        Assert.Equal(FindingVoteSide.Dig, finding.VoteBy(StubUser));
        Assert.Equal(FindingVoteSide.Bury, finding.VoteBy("dennis_r"));
        Assert.Null(finding.VoteBy("nobody_who_voted"));
    }

    [Fact]
    public async Task A_finding_without_thumbnail_or_promotion_rehydrates_its_absences()
    {
        var id = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");
        await GivenFindings(CreateFinding(id, "Still upcoming", promotedAt: null, thumbnail: null));

        var finding = await LookedUp(id);

        Assert.NotNull(finding);
        Assert.Null(finding.Thumbnail);
        Assert.Null(finding.PromotedAt);
        Assert.False(finding.IsPromoted);
    }

    [Fact]
    public async Task The_lookup_answers_null_when_no_finding_carries_the_id()
    {
        await GivenFindings(CreateFinding(Guid.NewGuid(), "Some other finding", At("2026-07-08T10:00:00Z")));

        Assert.Null(await LookedUp(Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666")));
    }

    [Fact]
    public async Task The_promoted_page_holds_only_promoted_findings_in_feed_order()
    {
        // Insertion order, title order, and creation order all differ from promotion order, so
        // a query ordering by any of those — or answering unpromoted findings — reads different.
        await GivenFindings(
            CreateFinding(Guid.NewGuid(), "B promoted at 10", At("2026-07-08T10:00:00Z")),
            CreateFinding(Guid.NewGuid(), "A still upcoming", promotedAt: null),
            CreateFinding(Guid.NewGuid(), "C promoted at 12", At("2026-07-08T12:00:00Z")),
            CreateFinding(Guid.NewGuid(), "D promoted at 11", At("2026-07-08T11:00:00Z")));

        var page = await PromotedPage(1, 25);

        Assert.Equal(
            ["C promoted at 12", "D promoted at 11", "B promoted at 10"],
            page.Select(finding => finding.Title).ToArray());
    }

    [Fact]
    public async Task Promotion_time_ties_break_by_id_descending()
    {
        // Findings promoted at the same instant need a deterministic secondary order, or items
        // could repeat or vanish across page boundaries.
        var promotedAt = At("2026-07-08T10:00:00Z");
        await GivenFindings(
            CreateFinding(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Tied low id", promotedAt),
            CreateFinding(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), "Tied high id", promotedAt));

        var page = await PromotedPage(1, 25);

        Assert.Equal(["Tied high id", "Tied low id"], page.Select(finding => finding.Title).ToArray());
    }

    [Fact]
    public async Task A_full_page_carries_one_extra_finding_as_the_next_page_signal()
    {
        await GivenFindings(FivePromoted());

        var page = await PromotedPage(1, 2);

        // Limit 2 with more behind it: two findings for the page plus exactly one look-ahead.
        Assert.Equal(
            ["Promoted 5", "Promoted 4", "Promoted 3"],
            page.Select(finding => finding.Title).ToArray());
    }

    [Fact]
    public async Task A_later_page_skips_the_earlier_pages()
    {
        await GivenFindings(FivePromoted());

        var page = await PromotedPage(2, 2);

        Assert.Equal(
            ["Promoted 3", "Promoted 2", "Promoted 1"],
            page.Select(finding => finding.Title).ToArray());
    }

    [Fact]
    public async Task The_last_page_holds_the_remainder_and_no_extra()
    {
        await GivenFindings(FivePromoted());

        var page = await PromotedPage(3, 2);

        Assert.Equal(["Promoted 1"], page.Select(finding => finding.Title).ToArray());
    }

    [Fact]
    public async Task A_page_past_the_end_answers_empty()
    {
        await GivenFindings(FivePromoted());

        Assert.Empty(await PromotedPage(4, 2));
    }

    private static Finding[] FivePromoted() =>
        Enumerable.Range(1, 5)
            .Select(hour => CreateFinding(
                Guid.NewGuid(), $"Promoted {hour}", At($"2026-07-08T{hour:00}:00:00Z")))
            .ToArray();
}
