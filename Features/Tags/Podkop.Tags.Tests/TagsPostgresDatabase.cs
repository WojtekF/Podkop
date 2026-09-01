using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Testing;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     The Tags slice's database for its behavior specs (issue #77): the shared PostgreSQL
///     fixture with this slice's schema brought up through the slice's own design-time factory —
///     the public seam that already carries the provider configuration — so specs run on exactly
///     the model and migrations the worker applies. The Findings schema comes up alongside it,
///     because the tag-page specs that reach the HTTP seam go through the composition root, and
///     hydrating a tag page means the findings it references have to exist. One instance serves
///     the whole collection; spec classes reset it before every spec, so suites in the collection
///     may freely reuse tags and content ids without stepping on each other.
/// </summary>
public sealed class TagsPostgresDatabase : PostgresTestDatabase
{
    protected override async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using (var findings = new FindingsDbContextFactory().CreateDbContext([connectionString]))
        {
            await findings.Database.MigrateAsync(cancellationToken);
        }

        await using var tags = new TagsDbContextFactory().CreateDbContext([connectionString]);
        await tags.Database.MigrateAsync(cancellationToken);
    }
}

[CollectionDefinition(Name)]
public sealed class TagsDatabaseCollection : ICollectionFixture<TagsPostgresDatabase>
{
    public const string Name = "tags database";
}
