using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

/// <summary>
///     The Findings slice's persistence shape (issue #67, ADR 0010), read off the model a host
///     actually builds: schema, identifier spelling, key, how the vote side and the bury reason
///     reach the database, and where applied migrations get recorded. Nothing here opens a
///     connection — a built model and the context's relational options answer every one of these
///     — so this class needs neither Docker nor orchestration; the aggregate's full round trip
///     is proven against real PostgreSQL by <see cref="EfFindingRepositoryTests" />.
/// </summary>
public class FindingsDbContextTests : IDisposable
{
    // Registration must not connect, so any well-formed connection string will do here. It is
    // deliberately not the orchestrated one: a test that only builds a model must never be able
    // to reach a real database by accident.
    private const string UnreachableConnectionString =
        "Host=localhost;Port=1;Database=podkopdb;Username=podkop;Password=podkop";

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--) _disposables[i].Dispose();
    }

    /// <summary>The context exactly as a host that called the slice's persistence entry point gets it.</summary>
    private FindingsDbContext RegisteredContext()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:podkopdb"] = UnreachableConnectionString
        });

        builder.AddFindingsPersistence();

        var host = builder.Build();
        _disposables.Add(host);
        var scope = host.Services.CreateScope();
        _disposables.Add(scope);
        return scope.ServiceProvider.GetRequiredService<FindingsDbContext>();
    }

    [Fact]
    public void The_findings_live_in_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var entityType = context.Model.FindEntityType(typeof(Finding));

        Assert.NotNull(entityType);
        Assert.Equal("findings", context.Model.GetDefaultSchema());
        Assert.Equal("findings", entityType.GetSchema());
    }

    [Fact]
    public void The_whole_model_stays_inside_the_slices_schema()
    {
        // Whatever shape the votes and tags take, no table of this context may land outside the
        // slice's schema (ADR 0010) — the boundary is real at the data layer, not per-table.
        using var context = RegisteredContext();

        Assert.All(
            context.Model.GetEntityTypes().Where(entityType => entityType.GetTableName() is not null),
            entityType => Assert.Equal("findings", entityType.GetSchema()));
    }

    [Fact]
    public void Tables_and_columns_are_spelled_the_way_the_database_spells_identifiers()
    {
        using var context = RegisteredContext();
        var entityType = context.Model.FindEntityType(typeof(Finding))!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

        Assert.Equal("findings", entityType.GetTableName());
        Assert.Equal("id", entityType.FindProperty(nameof(Finding.Id))!.GetColumnName(table));
        Assert.Equal("title", entityType.FindProperty(nameof(Finding.Title))!.GetColumnName(table));
        Assert.Equal("created_at", entityType.FindProperty(nameof(Finding.CreatedAt))!.GetColumnName(table));
        Assert.Equal("promoted_at", entityType.FindProperty(nameof(Finding.PromotedAt))!.GetColumnName(table));
        Assert.Equal("comment_count", entityType.FindProperty(nameof(Finding.CommentCount))!.GetColumnName(table));
    }

    [Fact]
    public void A_finding_is_keyed_by_its_id()
    {
        using var context = RegisteredContext();

        var key = context.Model.FindEntityType(typeof(Finding))!.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal([nameof(Finding.Id)], key.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void The_recorded_votes_are_part_of_the_model_with_their_enums_stored_as_names()
    {
        // Every recorded vote must be in the model — dig counts and the reader's own highlighted
        // vote are derived from them — and the side and the bury reason must reach the database
        // as their readable names, so values stay legible in psql and survive any future
        // reordering of the enums (ADR 0010). Presence is asserted first, so this can never pass
        // vacuously on a model that simply left the votes out.
        using var context = RegisteredContext();

        var enumProperties = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ClrType == typeof(FindingVoteSide)
                               || property.ClrType == typeof(BuryReason)
                               || property.ClrType == typeof(BuryReason?))
            .ToList();

        Assert.Contains(enumProperties, property => property.ClrType == typeof(FindingVoteSide));
        Assert.Contains(enumProperties,
            property => property.ClrType == typeof(BuryReason) || property.ClrType == typeof(BuryReason?));
        Assert.All(enumProperties, property =>
        {
            var storedType = property.GetProviderClrType() ?? property.GetValueConverter()?.ProviderClrType;
            Assert.True(
                storedType == typeof(string),
                $"{property.DeclaringType.DisplayName()}.{property.Name} must be stored as readable "
                + "text (ADR 0010), but the model stores it as "
                + $"{storedType?.Name ?? "the enum's underlying number"}.");
        });
    }

    [Fact]
    public void Applied_migrations_are_recorded_inside_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var relational = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());

        Assert.Equal("findings", relational.MigrationsHistoryTableSchema);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal(typeof(FindingsDbContext).Assembly.GetName().Name, relational.MigrationsAssembly);
    }
}
