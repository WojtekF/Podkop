using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.Tags.Domain;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     The membership index against real PostgreSQL (issue #77): the round trip, the paged read
///     the tag page runs on, and the existence question that separates a page from a 404. These
///     specs sit below the HTTP seam on purpose — the endpoint suite proves the contract, this one
///     proves the store answers it without dragging a tag's whole history into memory first.
/// </summary>
[Collection(TagsDatabaseCollection.Name)]
public class EfTagMembershipRepositoryTests(TagsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Guid Id(int index) => new($"00000000-0000-0000-0077-{index:D12}");

    private static TagMembership Membership(
        string tag, int contentIndex, string createdAt, TaggedContentType type = TaggedContentType.Finding) =>
        new(tag, type, Id(contentIndex), At(createdAt));

    private async Task Given(params TagMembership[] memberships)
    {
        await using var context = database.CreateDbContext();
        context.TagMemberships.AddRange(memberships);
        await context.SaveChangesAsync();
    }

    private static EfTagMembershipRepository RepositoryOver(Podkop.Tags.Infrastructure.TagsDbContext context) =>
        new(context);

    [Fact]
    public async Task A_membership_reads_back_exactly_as_it_was_written()
    {
        await Given(Membership("dotnet", 1, "2026-07-08T10:00:00Z", TaggedContentType.Entry));

        await using var context = database.CreateDbContext();
        var rows = await RepositoryOver(context)
            .GetForContentAsync(TaggedContentType.Entry, Id(1), CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("dotnet", row.Tag);
        Assert.Equal(TaggedContentType.Entry, row.ContentType);
        Assert.Equal(Id(1), row.ContentId);
        Assert.Equal(At("2026-07-08T10:00:00Z"), row.CreatedAt);
    }

    [Fact]
    public async Task The_rows_for_one_piece_of_content_are_every_tag_it_carries_and_nothing_else()
    {
        await Given(
            Membership("dotnet", 1, "2026-07-08T10:00:00Z"),
            Membership("webdev", 1, "2026-07-08T10:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T10:00:00Z"),
            Membership("dotnet", 1, "2026-07-08T10:00:00Z", TaggedContentType.Entry));

        await using var context = database.CreateDbContext();
        var rows = await RepositoryOver(context)
            .GetForContentAsync(TaggedContentType.Finding, Id(1), CancellationToken.None);

        Assert.Equal(["dotnet", "webdev"], rows.Select(row => row.Tag).Order().ToArray());
    }

    [Fact]
    public async Task A_tag_carried_by_nothing_is_carried_by_nothing()
    {
        await Given(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));

        await using var context = database.CreateDbContext();
        var repository = RepositoryOver(context);

        Assert.True(await repository.AnyContentCarriesAsync("dotnet", CancellationToken.None));
        Assert.False(await repository.AnyContentCarriesAsync("angular", CancellationToken.None));
    }

    [Fact]
    public async Task The_page_comes_back_newest_first_with_content_id_breaking_ties()
    {
        // Deliberately identical timestamps: without a tiebreak the order the database happens
        // to return is arbitrary, and a paged read whose order is arbitrary can repeat or skip
        // rows across a page turn.
        await Given(
            Membership("dotnet", 1, "2026-07-08T10:00:00Z"),
            Membership("dotnet", 3, "2026-07-08T10:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T10:00:00Z"));

        await using var context = database.CreateDbContext();
        var page = await RepositoryOver(context)
            .GetPageAsync("dotnet", null, 1, 10, CancellationToken.None);

        Assert.Equal([Id(3), Id(2), Id(1)], page.Select(row => row.ContentId).ToArray());
    }

    [Fact]
    public async Task The_page_answers_one_row_beyond_the_limit_as_the_next_page_signal()
    {
        // The caller tells a full last page from one with a successor without a second query.
        await Given([.. Enumerable.Range(1, 5).Select(i => Membership("dotnet", i, $"2026-07-08T{i + 8:00}:00:00Z"))]);

        await using var context = database.CreateDbContext();
        var repository = RepositoryOver(context);

        var full = await repository.GetPageAsync("dotnet", null, 1, 2, CancellationToken.None);
        var last = await repository.GetPageAsync("dotnet", null, 3, 2, CancellationToken.None);

        Assert.Equal(3, full.Count);
        Assert.Single(last);
    }

    [Fact]
    public async Task The_page_skips_the_pages_before_the_one_asked_for()
    {
        await Given([.. Enumerable.Range(1, 5).Select(i => Membership("dotnet", i, $"2026-07-08T{i + 8:00}:00:00Z"))]);

        await using var context = database.CreateDbContext();
        var page = await RepositoryOver(context)
            .GetPageAsync("dotnet", null, 2, 2, CancellationToken.None);

        Assert.Equal([Id(3), Id(2)], page.Take(2).Select(row => row.ContentId).ToArray());
    }

    [Fact]
    public async Task A_content_type_narrows_the_page_and_no_type_spans_them_all()
    {
        await Given(
            Membership("dotnet", 1, "2026-07-08T12:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T11:00:00Z", TaggedContentType.Entry));

        await using var context = database.CreateDbContext();
        var repository = RepositoryOver(context);

        var combined = await repository.GetPageAsync("dotnet", null, 1, 10, CancellationToken.None);
        var findings = await repository.GetPageAsync(
            "dotnet", TaggedContentType.Finding, 1, 10, CancellationToken.None);

        Assert.Equal([Id(1), Id(2)], combined.Select(row => row.ContentId).ToArray());
        Assert.Equal([Id(1)], findings.Select(row => row.ContentId).ToArray());
    }

    [Fact]
    public async Task A_page_past_the_end_comes_back_empty()
    {
        await Given(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));

        await using var context = database.CreateDbContext();
        var page = await RepositoryOver(context)
            .GetPageAsync("dotnet", null, 9, 10, CancellationToken.None);

        Assert.Empty(page);
    }

    [Fact]
    public async Task Adding_and_removing_are_durable_only_once_the_unit_of_work_commits()
    {
        // The repository persists nothing itself (issue #96's pattern): everything it tracks
        // lands in the unit of work's one explicit commit.
        await using (var writing = database.CreateDbContext())
        {
            RepositoryOver(writing).Add(Membership("dotnet", 1, "2026-07-08T10:00:00Z"));
            await using var uncommitted = database.CreateDbContext();
            Assert.Empty(await uncommitted.TagMemberships.AsNoTracking().ToListAsync());

            await new EfUnitOfWork(writing).CommitAsync(CancellationToken.None);
        }

        await using var reading = database.CreateDbContext();
        Assert.Single(await reading.TagMemberships.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Removing_takes_out_exactly_the_rows_it_was_given()
    {
        await Given(
            Membership("dotnet", 1, "2026-07-08T10:00:00Z"),
            Membership("webdev", 1, "2026-07-08T10:00:00Z"),
            Membership("dotnet", 2, "2026-07-08T10:00:00Z"));

        await using (var writing = database.CreateDbContext())
        {
            var repository = RepositoryOver(writing);
            var doomed = await repository.GetForContentAsync(
                TaggedContentType.Finding, Id(1), CancellationToken.None);
            repository.RemoveRange(doomed);
            await new EfUnitOfWork(writing).CommitAsync(CancellationToken.None);
        }

        await using var reading = database.CreateDbContext();
        var left = await reading.TagMemberships.AsNoTracking().ToListAsync();
        Assert.Equal([Id(2)], left.Select(row => row.ContentId).ToArray());
    }
}
