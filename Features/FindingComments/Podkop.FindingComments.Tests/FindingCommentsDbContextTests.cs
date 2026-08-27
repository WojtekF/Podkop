using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The FindingComments slice's persistence shape (issue #68, ADR 0010), read off the model a
///     host actually builds: schema, identifier spelling, key, how the vote directions reach the
///     database, that the finding reference crosses no schema boundary, and where applied
///     migrations get recorded. Nothing here opens a connection — a built model and the context's
///     relational options answer every one of these — so this class needs neither Docker nor
///     orchestration; the comment's full round trip is proven against real PostgreSQL by
///     <see cref="EfCommentRepositoryTests" />.
/// </summary>
public class FindingCommentsDbContextTests : IDisposable
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
    private FindingCommentsDbContext RegisteredContext()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:podkopdb"] = UnreachableConnectionString
        });

        builder.AddFindingCommentsPersistence();

        var host = builder.Build();
        _disposables.Add(host);
        var scope = host.Services.CreateScope();
        _disposables.Add(scope);
        return scope.ServiceProvider.GetRequiredService<FindingCommentsDbContext>();
    }

    [Fact]
    public void The_comments_live_in_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var entityType = context.Model.FindEntityType(typeof(Comment));

        Assert.NotNull(entityType);
        Assert.Equal("finding_comments", context.Model.GetDefaultSchema());
        Assert.Equal("finding_comments", entityType.GetSchema());
    }

    [Fact]
    public void The_whole_model_stays_inside_the_slices_schema()
    {
        // Whatever shape the votes take, no table of this context may land outside the slice's
        // schema (ADR 0010) — the boundary is real at the data layer, not per-table.
        using var context = RegisteredContext();

        Assert.All(
            context.Model.GetEntityTypes().Where(entityType => entityType.GetTableName() is not null),
            entityType => Assert.Equal("finding_comments", entityType.GetSchema()));
    }

    [Fact]
    public void Tables_and_columns_are_spelled_the_way_the_database_spells_identifiers()
    {
        using var context = RegisteredContext();
        var entityType = context.Model.FindEntityType(typeof(Comment))!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

        Assert.Equal("comments", entityType.GetTableName());
        Assert.Equal("id", entityType.FindProperty(nameof(Comment.Id))!.GetColumnName(table));
        Assert.Equal("finding_id", entityType.FindProperty(nameof(Comment.FindingId))!.GetColumnName(table));
        Assert.Equal("parent_comment_id",
            entityType.FindProperty(nameof(Comment.ParentCommentId))!.GetColumnName(table));
        Assert.Equal("author", entityType.FindProperty(nameof(Comment.Author))!.GetColumnName(table));
        Assert.Equal("text", entityType.FindProperty(nameof(Comment.Text))!.GetColumnName(table));
        Assert.Equal("created_at", entityType.FindProperty(nameof(Comment.CreatedAt))!.GetColumnName(table));
    }

    [Fact]
    public void A_comment_is_keyed_by_its_id()
    {
        using var context = RegisteredContext();

        var key = context.Model.FindEntityType(typeof(Comment))!.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal([nameof(Comment.Id)], key.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void The_finding_reference_stays_a_plain_column_no_relationship_leaves_the_schema()
    {
        // ADR 0010: no cross-schema foreign keys. The comment's finding lives in the Findings
        // slice's schema, so its reference must be a plain uuid column — every relationship the
        // model does declare (however the votes are shaped) must stay inside this slice's schema.
        using var context = RegisteredContext();

        var comment = context.Model.FindEntityType(typeof(Comment))!;
        Assert.NotNull(comment.FindProperty(nameof(Comment.FindingId)));
        Assert.All(
            context.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()),
            foreignKey => Assert.Equal("finding_comments", foreignKey.PrincipalEntityType.GetSchema()));
    }

    [Fact]
    public void The_recorded_votes_are_part_of_the_model_with_their_direction_stored_as_names()
    {
        // Every recorded vote must be in the model — both counts and the reader's own highlighted
        // vote are derived from them — and the direction must reach the database as its readable
        // name, so values stay legible in psql and survive any future reordering of the enum
        // (ADR 0010). Presence is asserted first, so this can never pass vacuously on a model
        // that simply left the votes out.
        using var context = RegisteredContext();

        var directionProperties = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ClrType == typeof(VoteDirection))
            .ToList();

        Assert.NotEmpty(directionProperties);
        Assert.All(directionProperties, property =>
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

        Assert.Equal("finding_comments", relational.MigrationsHistoryTableSchema);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal(typeof(FindingCommentsDbContext).Assembly.GetName().Name, relational.MigrationsAssembly);
    }
}
