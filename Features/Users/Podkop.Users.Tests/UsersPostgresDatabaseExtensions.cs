using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

public static class UsersPostgresDatabaseExtensions
{
    public static UsersDbContext CreateDbContext(this UsersPostgresDatabase database) =>
        new UsersDbContextFactory().CreateDbContext([database.ConnectionString]);
}
