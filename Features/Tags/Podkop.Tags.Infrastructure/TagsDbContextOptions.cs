using Microsoft.EntityFrameworkCore;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The provider facts the running host and the EF command-line tooling must agree on (issue
///     #77, ADR 0010). Both halves of the slice's persistence configure the context through here,
///     so a migration added from the command line can never land somewhere the host does not look
///     for it, and neither half can start spelling identifiers differently from the other.
/// </summary>
internal static class TagsDbContextOptions
{
    public const string Schema = "tags";

    public static DbContextOptionsBuilder UseTagsPostgres(
        this DbContextOptionsBuilder options,
        string? connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(TagsDbContext).Assembly.GetName().Name)
                .MigrationsHistoryTable("__EFMigrationsHistory", Schema))
            .UseSnakeCaseNamingConvention();
}
