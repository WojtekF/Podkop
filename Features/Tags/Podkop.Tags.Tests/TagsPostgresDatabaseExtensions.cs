using Podkop.Findings.Infrastructure;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

public static class TagsPostgresDatabaseExtensions
{
    public static TagsDbContext CreateDbContext(this TagsPostgresDatabase database) =>
        new TagsDbContextFactory().CreateDbContext([database.ConnectionString]);

    /// <summary>
    ///     The Findings slice's context over the same test database — how a spec puts the
    ///     findings a tag page's references hydrate to in place. A Tests project may reach into
    ///     another slice to seed the composition root; production code never may (ADR 0003).
    /// </summary>
    public static FindingsDbContext CreateFindingsDbContext(this TagsPostgresDatabase database) =>
        new FindingsDbContextFactory().CreateDbContext([database.ConnectionString]);
}
