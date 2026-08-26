using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Testing;

namespace Podkop.Findings.Tests;

/// <summary>
///     The Findings slice's database for its behavior specs (issue #67): the shared PostgreSQL
///     fixture with this slice's schema brought up through the slice's own design-time factory —
///     the public seam that already carries the provider configuration — so specs run on exactly
///     the model and migrations the worker applies. One instance serves the whole collection;
///     spec classes reset it before every spec, so suites in the collection may freely reuse
///     finding ids without stepping on each other.
/// </summary>
public sealed class FindingsPostgresDatabase : PostgresTestDatabase
{
    protected override async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = new FindingsDbContextFactory().CreateDbContext([connectionString]);
        await context.Database.MigrateAsync(cancellationToken);
    }
}

[CollectionDefinition(Name)]
public sealed class FindingsDatabaseCollection : ICollectionFixture<FindingsPostgresDatabase>
{
    public const string Name = "findings database";
}
