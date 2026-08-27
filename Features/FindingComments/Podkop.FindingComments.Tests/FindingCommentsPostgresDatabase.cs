using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     This suite's database (issue #68): the shared PostgreSQL fixture with BOTH slices' schemas
///     brought up — the discussions this slice persists hang off findings that live in the
///     Findings slice's schema, so the HTTP seam can only be exercised with both sides real.
///     Each schema comes up through its slice's own design-time factory — the same models and
///     migrations the worker applies, findings first, the worker's order. Reaching into the
///     Findings slice's Infrastructure is the composition-root test exception, never a production
///     dependency. One instance serves the whole collection; spec classes reset it before every
///     spec.
/// </summary>
public sealed class FindingCommentsPostgresDatabase : PostgresTestDatabase
{
    protected override async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using (var findings = new FindingsDbContextFactory().CreateDbContext([connectionString]))
        {
            await findings.Database.MigrateAsync(cancellationToken);
        }

        await using var comments = new FindingCommentsDbContextFactory().CreateDbContext([connectionString]);
        await comments.Database.MigrateAsync(cancellationToken);
    }

    public FindingsDbContext CreateFindingsDbContext() =>
        new FindingsDbContextFactory().CreateDbContext([ConnectionString]);

    public FindingCommentsDbContext CreateDbContext() =>
        new FindingCommentsDbContextFactory().CreateDbContext([ConnectionString]);
}

[CollectionDefinition(Name)]
public sealed class FindingCommentsDatabaseCollection : ICollectionFixture<FindingCommentsPostgresDatabase>
{
    public const string Name = "finding comments database";
}
