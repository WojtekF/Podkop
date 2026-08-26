using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

public static class FindingsPostgresDatabaseExtensions
{
    public static FindingsDbContext CreateDbContext(this FindingsPostgresDatabase database) =>
        new FindingsDbContextFactory().CreateDbContext([database.ConnectionString]);
}
