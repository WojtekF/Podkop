using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

/// <summary>
///     What the shared PostgreSQL fixture promises this slice's consumers (issue #67): before
///     any spec runs the database holds the slice's schema — applied from the slice's own
///     checked-in migrations, the way the worker applies them — and a reset wipes the rows a
///     spec put in while leaving the schema and its recorded migration history untouched, so
///     specs start empty without the schema ever being rebuilt.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingsDatabaseFixtureTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private FindingsDbContext NewContext() => database.CreateDbContext();

    [Fact]
    public async Task The_database_comes_up_with_the_slices_migrations_applied()
    {
        await using var context = NewContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        Assert.NotEmpty(applied);
    }

    [Fact]
    public async Task A_reset_wipes_the_rows_but_keeps_the_schema_and_its_history()
    {
        await using (var context = NewContext())
        {
            context.Findings.Add(new Finding(
                Guid.NewGuid(),
                "A finding the reset takes away",
                "Written only to be wiped.",
                new Uri("https://example.org/articles/1"),
                null,
                "grace_hopper",
                ["dotnet"],
                DateTimeOffset.Parse("2026-07-01T00:00:00Z", CultureInfo.InvariantCulture),
                null,
                0));
            await context.SaveChangesAsync();
        }

        await database.ResetAsync();

        await using var reading = NewContext();
        Assert.Empty(await reading.Findings.AsNoTracking().ToListAsync());
        // The history table survived the reset: a reset that truncated it would leave the next
        // migration run trying to re-apply what the schema already holds.
        Assert.NotEmpty(await reading.Database.GetAppliedMigrationsAsync());
    }
}
