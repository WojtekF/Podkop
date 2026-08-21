using Microsoft.EntityFrameworkCore;

namespace Podkop.Users.Infrastructure;

/// <summary>
///     The provider facts the running host and the EF command-line tooling must agree on (issue
///     #88, ADR 0010). Both halves of the slice's persistence configure the context through here,
///     so a migration added from the command line can never land somewhere the host does not look
///     for it, and neither half can start spelling identifiers differently from the other.
/// </summary>
internal static class UsersDbContextOptions
{
    public const string Schema = "users";

    public static DbContextOptionsBuilder UseUsersPostgres(
        this DbContextOptionsBuilder options,
        string? connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(UsersDbContext).Assembly.GetName().Name)
                .MigrationsHistoryTable("__EFMigrationsHistory", Schema))
            .UseSnakeCaseNamingConvention();
}
