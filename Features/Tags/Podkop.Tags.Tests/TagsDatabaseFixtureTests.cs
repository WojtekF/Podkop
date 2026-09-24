using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Tests;

/// <summary>
///     What the shared PostgreSQL fixture promises this suite (issue #77): before any spec runs
///     the database holds BOTH schemas the tag page needs — the Tags index and the Findings its
///     references hydrate to, each applied from its slice's own checked-in migrations the way the
///     worker applies them — and a reset wipes the rows a spec put in while leaving the schemas
///     and their recorded migration histories untouched, so specs start empty without a schema
///     ever being rebuilt.
/// </summary>
[Collection(TagsDatabaseCollection.Name)]
public class TagsDatabaseFixtureTests(TagsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_database_comes_up_with_both_slices_migrations_applied()
    {
        await using var tags = database.CreateDbContext();
        Assert.NotEmpty(await tags.Database.GetAppliedMigrationsAsync());

        await using var findings = database.CreateFindingsDbContext();
        Assert.NotEmpty(await findings.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task A_reset_wipes_the_rows_but_keeps_the_schema_and_its_history()
    {
        await using (var context = database.CreateDbContext())
        {
            context.TagMemberships.Add(new TagMembership(
                "dotnet",
                TaggedContentType.Finding,
                Guid.NewGuid(),
                DateTimeOffset.Parse("2026-07-01T00:00:00Z", CultureInfo.InvariantCulture)));
            await context.SaveChangesAsync();
        }

        await database.ResetAsync();

        await using var reading = database.CreateDbContext();
        Assert.Empty(await reading.TagMemberships.AsNoTracking().ToListAsync());
        // The history table survived the reset: a reset that truncated it would leave the next
        // migration run trying to re-apply what the schema already holds.
        Assert.NotEmpty(await reading.Database.GetAppliedMigrationsAsync());
    }
}
