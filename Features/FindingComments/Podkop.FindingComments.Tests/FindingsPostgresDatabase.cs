using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The Findings slice's database, brought up for this suite's seed-coherence specs (issue
///     #67): findings live in PostgreSQL now, so the pact between the seeded discussions and the
///     counts the findings advertise (issue #16) can only be observed with the findings side in
///     a real database. The schema comes up through the Findings slice's own design-time factory
///     — the same model and migrations the worker applies; reaching into another slice's
///     Infrastructure is the composition-root test exception, never a production dependency.
/// </summary>
public sealed class FindingsPostgresDatabase : PostgresTestDatabase
{
    protected override async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = new FindingsDbContextFactory().CreateDbContext([connectionString]);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public FindingsDbContext CreateDbContext() =>
        new FindingsDbContextFactory().CreateDbContext([ConnectionString]);
}

[CollectionDefinition(Name)]
public sealed class FindingsDatabaseCollection : ICollectionFixture<FindingsPostgresDatabase>
{
    public const string Name = "findings database";
}
