using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     The Development-only database seed for findings (issue #67), invoked by
///     <c>Podkop.MigrationService</c> after this slice's migrations are applied: it puts the
///     given sample findings — votes, tags, and comment counts included — into the slice's
///     tables, so a fresh data volume comes up with the Main Page the running app already shows.
///     The orchestrated database keeps its data across restarts and this step runs on every
///     start, so a run that finds records already there must leave them exactly as they are
///     instead of adding a second copy: repeated runs must not make the population grow or
///     change.
/// </summary>
public static class FindingsSeed
{
    public static async Task SeedAsync(
        FindingsDbContext context,
        IReadOnlyList<Finding> findings,
        CancellationToken cancellationToken)
    {
        if (await context.Findings.AnyAsync(cancellationToken)) return;
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync(cancellationToken);
    }
}
