using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.Tags.Contracts;
using Podkop.Tags.Domain;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     The Development seed for the membership index (issue #77). The index is normally built
///     only by consuming announce events, and the sample content is written straight into its own
///     slice's tables by the worker rather than announced — so the seed stands in for the
///     announcements that never happened and must land the index in exactly the state consuming
///     them would have. The orchestrated database keeps its data across restarts and the worker
///     seeds on every start, so the whole thing has to be idempotent.
/// </summary>
[Collection(TagsDatabaseCollection.Name)]
public class TagsDatabaseSeedTests(TagsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Guid Id(int index) => new($"00000000-0000-0000-0077-{index:D12}");

    private static SampleTaggedContent Content(int index, string[] tags, string createdAt) =>
        new(TaggedContentTypes.Finding, Id(index), tags, At(createdAt));

    private async Task Seed(IReadOnlyList<TagMembership> memberships)
    {
        await using var context = database.CreateDbContext();
        await TagsSeed.SeedAsync(context, memberships, CancellationToken.None);
    }

    private async Task<IReadOnlyList<TagMembership>> IndexAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.TagMemberships.AsNoTracking().ToListAsync();
    }

    [Fact]
    public void The_generator_files_every_piece_of_content_under_every_tag_it_carries()
    {
        var rows = SampleTagMemberships.GenerateFor(
        [
            Content(1, ["dotnet", "webdev"], "2026-07-08T10:00:00Z"),
            Content(2, ["dotnet"], "2026-07-08T11:00:00Z"),
        ]);

        Assert.Equal(3, rows.Count);
        Assert.Equal(
            ["dotnet", "webdev"],
            rows.Where(row => row.ContentId == Id(1)).Select(row => row.Tag).Order().ToArray());
    }

    [Fact]
    public void The_generator_carries_each_contents_own_creation_time()
    {
        // What makes the seeded tag pages come up in the same Newest order a live one would.
        var rows = SampleTagMemberships.GenerateFor([Content(1, ["dotnet"], "2026-06-01T08:30:00Z")]);

        Assert.Equal(At("2026-06-01T08:30:00Z"), Assert.Single(rows).CreatedAt);
        Assert.Equal(TaggedContentType.Finding, rows[0].ContentType);
    }

    [Fact]
    public void The_generator_folds_the_tags_the_way_a_real_write_would()
    {
        var rows = SampleTagMemberships.GenerateFor([Content(1, ["DotNet", "web-dev"], "2026-07-08T10:00:00Z")]);

        Assert.Equal(["dotnet", "webdev"], rows.Select(row => row.Tag).Order().ToArray());
    }

    [Fact]
    public void The_generator_is_deterministic_across_calls()
    {
        // The two sides of the seed pact generate in different processes and nothing lines them
        // up afterwards (the SampleSeed pact), so the same content in must mean the same rows out.
        var content = new[] { Content(1, ["dotnet", "webdev"], "2026-07-08T10:00:00Z") };

        var first = SampleTagMemberships.GenerateFor(content);
        var second = SampleTagMemberships.GenerateFor(content);

        Assert.Equal(
            first.Select(row => (row.Tag, row.ContentType, row.ContentId, row.CreatedAt)),
            second.Select(row => (row.Tag, row.ContentType, row.ContentId, row.CreatedAt)));
    }

    [Fact]
    public async Task Seeding_an_empty_index_puts_the_rows_in()
    {
        await Seed(SampleTagMemberships.GenerateFor([Content(1, ["dotnet", "webdev"], "2026-07-08T10:00:00Z")]));

        Assert.Equal(2, (await IndexAsync()).Count);
    }

    [Fact]
    public async Task Seeding_twice_leaves_the_index_exactly_as_the_first_run_left_it()
    {
        // The worker seeds on every start against a data volume that survives restarts.
        var rows = SampleTagMemberships.GenerateFor([Content(1, ["dotnet", "webdev"], "2026-07-08T10:00:00Z")]);
        await Seed(rows);
        var afterFirstRun = await IndexAsync();

        await Seed(rows);

        var afterSecondRun = await IndexAsync();
        Assert.Equal(
            afterFirstRun.Select(row => (row.Tag, row.ContentId)).Order(),
            afterSecondRun.Select(row => (row.Tag, row.ContentId)).Order());
    }
}
