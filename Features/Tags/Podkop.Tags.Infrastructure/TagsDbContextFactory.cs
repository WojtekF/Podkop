using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     How the EF command-line tooling builds this context when nothing is orchestrated (issue
///     #77): the real connection string only exists at run time inside Aspire, so design-time
///     commands need one of their own — a local default, overridable by the first command-line
///     argument — and the same migrations placement the running host uses, so a migration added
///     from the command line is written into this slice's assembly and recorded in this slice's
///     history table rather than the database-wide default. Adding a migration never touches the
///     database; only applying one does, which is the migration worker's job.
/// </summary>
public sealed class TagsDbContextFactory : IDesignTimeDbContextFactory<TagsDbContext>
{
    // Design-time commands never connect — adding a migration only needs a well-formed string.
    private const string LocalDefaultConnectionString =
        "Host=localhost;Port=5432;Database=podkopdb;Username=postgres;Password=postgres";

    public TagsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TagsDbContext>();
        options.UseTagsPostgres(args.FirstOrDefault() ?? LocalDefaultConnectionString);
        return new TagsDbContext(options.Options);
    }
}
