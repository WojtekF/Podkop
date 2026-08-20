using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

/// <summary>
///     The Development-only database seed for user records (issue #88), invoked by
///     <c>Podkop.MigrationService</c> after this slice's migrations are applied: it puts the
///     deterministic sample users — the same ones <see cref="SampleUsers" /> hands the API host
///     in memory — into the slice's tables, so a fresh data volume comes up knowing the people
///     the running app already knows. The orchestrated database keeps its data across restarts
///     and this step runs on every start, so a run that finds records already there must leave
///     them exactly as they are instead of adding a second copy: repeated runs must not make the
///     population grow or change. The orchestration suite in <c>Podkop.AppHost.Tests</c> boots
///     the graph twice and compares.
/// </summary>
public static class UsersSeed
{
    public static Task SeedAsync(
        UsersDbContext context,
        IReadOnlyList<User> users,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
