using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Podkop.Tags.Domain;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     The design-time half of the slice's persistence (issue #77): the EF command-line tooling
///     has to be able to build this context with no orchestration running — that is the only way a
///     migration gets added — and it has to build it with the same migrations placement the
///     running host uses, or a migration added from the command line lands in the wrong assembly
///     and is recorded in the wrong history table. The last spec is the one that stays red until
///     the initial migration is generated and checked in.
/// </summary>
public class TagsDesignTimeFactoryTests
{
    [Fact]
    public void The_tooling_can_build_the_context_with_nothing_orchestrated()
    {
        using var context = new TagsDbContextFactory().CreateDbContext([]);

        Assert.Equal("tags", context.Model.GetDefaultSchema());
        Assert.NotNull(context.Model.FindEntityType(typeof(TagMembership)));
    }

    [Fact]
    public void The_tooling_writes_migrations_where_the_running_host_looks_for_them()
    {
        using var context = new TagsDbContextFactory().CreateDbContext([]);

        var relational = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());

        Assert.Equal(typeof(TagsDbContext).Assembly.GetName().Name, relational.MigrationsAssembly);
        Assert.Equal("tags", relational.MigrationsHistoryTableSchema);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
    }

    [Fact]
    public void An_initial_migration_is_checked_in()
    {
        using var context = new TagsDbContextFactory().CreateDbContext([]);

        var migrations = context.Database.GetMigrations().ToArray();

        Assert.True(
            migrations.Length > 0,
            "The slice ships no migration, so a fresh database would come up without a tags "
            + "schema: the migration worker applies migrations, it never creates schemas on the "
            + "fly. Generate the initial migration into this slice's own project and check it in.");
    }
}
