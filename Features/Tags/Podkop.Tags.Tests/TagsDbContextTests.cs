using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Domain;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     The Tags slice's persistence shape (issue #77, ADR 0010), read off the model a host
///     actually builds: schema, identifier spelling, what identifies a membership, how the content
///     type reaches the database, and where applied migrations get recorded. Nothing here opens a
///     connection — a built model and the context's relational options answer every one of these —
///     so this class needs neither Docker nor orchestration; the round trip is proven against real
///     PostgreSQL by <see cref="EfTagMembershipRepositoryTests" />.
/// </summary>
public class TagsDbContextTests : IDisposable
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
    private TagsDbContext RegisteredContext()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:podkopdb"] = UnreachableConnectionString
        });

        builder.AddTagsPersistence();

        var host = builder.Build();
        _disposables.Add(host);
        var scope = host.Services.CreateScope();
        _disposables.Add(scope);
        return scope.ServiceProvider.GetRequiredService<TagsDbContext>();
    }

    [Fact]
    public void The_membership_index_lives_in_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var entityType = context.Model.FindEntityType(typeof(TagMembership));

        Assert.NotNull(entityType);
        Assert.Equal("tags", context.Model.GetDefaultSchema());
        Assert.Equal("tags", entityType.GetSchema());
    }

    [Fact]
    public void The_whole_model_stays_inside_the_slices_schema()
    {
        // Whatever shape the index and the inbox take, no table of this context may land outside
        // the slice's schema (ADR 0010) — the boundary is real at the data layer, not per-table.
        using var context = RegisteredContext();

        Assert.All(
            context.Model.GetEntityTypes().Where(entityType => entityType.GetTableName() is not null),
            entityType => Assert.Equal("tags", entityType.GetSchema()));
    }

    [Fact]
    public void Identifiers_are_spelled_the_way_the_ADR_spells_them()
    {
        // Nothing in psql should need quoting (ADR 0010).
        using var context = RegisteredContext();

        var entityType = context.Model.FindEntityType(typeof(TagMembership))!;

        Assert.Equal("tag_memberships", entityType.GetTableName());
        Assert.All(
            entityType.GetProperties().Select(property => property.GetColumnName()),
            column => Assert.Equal(column.ToLowerInvariant(), column));
        Assert.Contains("content_id", entityType.GetProperties().Select(property => property.GetColumnName()));
        Assert.Contains("created_at", entityType.GetProperties().Select(property => property.GetColumnName()));
    }

    [Fact]
    public void A_membership_is_identified_by_the_three_facts_that_make_it_one()
    {
        // No surrogate key: a piece of content carries a tag once, and the database is what has
        // to enforce that, so a redelivered announcement can never file the same row twice.
        using var context = RegisteredContext();

        var key = context.Model.FindEntityType(typeof(TagMembership))!.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(
            [nameof(TagMembership.ContentId), nameof(TagMembership.ContentType), nameof(TagMembership.Tag)],
            key.Properties.Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public void The_content_type_reaches_the_database_as_its_readable_name()
    {
        // ADR 0010: values stay legible in psql and survive any future reordering of the enum.
        using var context = RegisteredContext();

        var contentType = context.Model.FindEntityType(typeof(TagMembership))!
            .FindProperty(nameof(TagMembership.ContentType))!;

        Assert.Equal(typeof(string), contentType.GetProviderClrType());
    }

    [Fact]
    public void The_index_is_reachable_by_tag_without_reading_the_whole_table()
    {
        // A tag page is "one tag's rows, newest first, skip deep" — the query the slice runs on
        // every page load, so the model owes it an index rather than a sequential scan.
        using var context = RegisteredContext();

        var indexes = context.Model.FindEntityType(typeof(TagMembership))!.GetIndexes()
            .Select(index => index.Properties.Select(property => property.Name).ToArray())
            .ToArray();

        Assert.Contains(indexes, columns => columns.First() == nameof(TagMembership.Tag));
    }

    [Fact]
    public void The_slice_keeps_its_own_inbox()
    {
        // ADR 0014: a shared inbox table would be a cross-slice write dependency, so every
        // consuming slice stores the identical shape in its own schema.
        using var context = RegisteredContext();

        var inbox = context.Model.FindEntityType(typeof(InboxMessage));

        Assert.NotNull(inbox);
        Assert.Equal("inbox_messages", inbox.GetTableName());
        Assert.Equal("tags", inbox.GetSchema());
    }

    [Fact]
    public void The_slice_keeps_no_outbox_because_it_announces_nothing()
    {
        // Tags is a pure consumer of the tag namespace (ADR 0009): everything it writes is the
        // effect of somebody else's announcement, and nobody downstream is waiting to hear it.
        using var context = RegisteredContext();

        Assert.Null(context.Model.FindEntityType(typeof(OutboxMessage)));
    }

    [Fact]
    public void Applied_migrations_are_recorded_inside_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var relational = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());

        Assert.Equal("tags", relational.MigrationsHistoryTableSchema);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal(typeof(TagsDbContext).Assembly.GetName().Name, relational.MigrationsAssembly);
    }
}
