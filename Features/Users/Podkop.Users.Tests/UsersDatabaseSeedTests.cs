using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The database seed's own rule (issue #88): a fresh database receives the deterministic
///     sample users, and a run that finds records already there leaves them exactly as it found
///     them — the orchestrated database keeps its data across restarts and the worker seeds on
///     every start, so repeated runs must not make the population grow or change. The physical
///     facts (schema, identifier spelling, stored role type) belong to the model specs and the
///     orchestration suite; nothing here depends on them, so the machinery runs on Sqlite and
///     the suite stays free of a container runtime — the carve-out
///     <c>Podkop.MigrationService.Tests</c> already takes for the migrate step.
/// </summary>
public sealed class UsersDatabaseSeedTests : IDisposable
{
    // One open connection held for the fixture's lifetime: Sqlite discards an in-memory database
    // when its last connection closes, and each seed run below wants its own context over the
    // same database, the way the worker resolves a fresh context per scope.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public UsersDatabaseSeedTests()
    {
        _connection.Open();
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private UsersDbContext NewContext() =>
        new(new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(_connection).Options);

    private static (string UserName, string Role)[] PopulationOf(UsersDbContext context) =>
        context.Users
            .AsNoTracking()
            .Select(user => new { user.UserName, user.Role })
            .ToArray()
            .Select(user => (user.UserName, user.Role.ToString()))
            .OrderBy(user => user.UserName, StringComparer.Ordinal)
            .ToArray();

    private static (string UserName, string Role)[] Expected(IEnumerable<User> users) =>
        users
            .Select(user => (user.UserName, Role: user.Role.ToString()))
            .OrderBy(user => user.UserName, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public async Task A_fresh_database_receives_every_sample_user_with_its_role()
    {
        var sampleUsers = SampleUsers.Generate();

        await using (var seeding = NewContext())
        {
            await UsersSeed.SeedAsync(seeding, sampleUsers, CancellationToken.None);
        }

        await using var reading = NewContext();
        Assert.Equal(Expected(sampleUsers), PopulationOf(reading));
    }

    [Fact]
    public async Task A_second_run_leaves_the_population_exactly_as_it_found_it()
    {
        var sampleUsers = SampleUsers.Generate();
        await using (var first = NewContext())
        {
            await UsersSeed.SeedAsync(first, sampleUsers, CancellationToken.None);
        }

        await using (var second = NewContext())
        {
            await UsersSeed.SeedAsync(second, sampleUsers, CancellationToken.None);
        }

        await using var reading = NewContext();
        Assert.Equal(Expected(sampleUsers), PopulationOf(reading));
    }

    /// <summary>
    ///     The skip is decided by the population being there at all, not by comparing it against
    ///     the sample set: a database that already holds records keeps exactly those, even when
    ///     the sample users have moved on since. Spelled out because it is the behaviour a kept
    ///     data volume shows after the sample vocabulary changes — the seed will not reconcile
    ///     it, and the volume has to go for the new population to land.
    /// </summary>
    [Fact]
    public async Task A_run_that_finds_records_keeps_them_even_when_the_sample_users_have_changed()
    {
        var alreadyThere = new User("someone_from_an_older_run", UserRole.Member);
        await using (var populating = NewContext())
        {
            populating.Users.Add(alreadyThere);
            await populating.SaveChangesAsync(CancellationToken.None);
        }

        await using (var seeding = NewContext())
        {
            await UsersSeed.SeedAsync(seeding, SampleUsers.Generate(), CancellationToken.None);
        }

        await using var reading = NewContext();
        Assert.Equal(Expected([alreadyThere]), PopulationOf(reading));
    }
}
