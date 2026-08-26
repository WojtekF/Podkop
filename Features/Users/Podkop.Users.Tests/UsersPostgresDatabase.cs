using Microsoft.EntityFrameworkCore;
using Podkop.Shared.Testing;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The Users slice's database for its behavior specs (issue #89): the shared PostgreSQL
///     fixture with this slice's schema brought up through the slice's own design-time factory —
///     the public seam that already carries the provider configuration — so specs run on exactly
///     the model and migrations the worker applies. One instance serves the whole collection;
///     spec classes reset it before every spec, so suites in the collection may freely reuse
///     usernames without stepping on each other.
/// </summary>
public sealed class UsersPostgresDatabase : PostgresTestDatabase
{
    protected override async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = new UsersDbContextFactory().CreateDbContext([connectionString]);
        await context.Database.MigrateAsync(cancellationToken);
    }
}

[CollectionDefinition(Name)]
public sealed class UsersDatabaseCollection : ICollectionFixture<UsersPostgresDatabase>
{
    public const string Name = "users database";
}
