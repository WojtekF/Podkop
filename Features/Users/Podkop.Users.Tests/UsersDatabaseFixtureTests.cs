using Microsoft.EntityFrameworkCore;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     What the shared PostgreSQL fixture promises its consumers (issue #89), pinned on its
///     first one: before any spec runs the database holds the slice's schema — applied from the
///     slice's own checked-in migrations, the way the worker applies them — and a reset wipes
///     the rows a spec put in while leaving the schema and its recorded migration history
///     untouched, so specs start empty without the schema ever being rebuilt.
/// </summary>
[Collection(UsersDatabaseCollection.Name)]
public class UsersDatabaseFixtureTests(UsersPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private UsersDbContext NewContext() => database.CreateDbContext();

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
            context.Users.Add(new User("ada_lovelace", UserRole.Member));
            await context.SaveChangesAsync();
        }

        await database.ResetAsync();

        await using var reading = NewContext();
        Assert.Empty(await reading.Users.AsNoTracking().ToListAsync());
        // The history table survived the reset: a reset that truncated it would leave the next
        // migration run trying to re-apply what the schema already holds.
        Assert.NotEmpty(await reading.Database.GetAppliedMigrationsAsync());
    }
}
