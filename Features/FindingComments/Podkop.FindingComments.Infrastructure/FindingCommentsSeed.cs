using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     The Development-only database seed for the sample discussions (issue #68), invoked by
///     <c>Podkop.MigrationService</c> after this slice's migrations are applied — and after the
///     Findings seed, because every seeded comment hangs off a finding the database must already
///     hold. It puts the given sample comments — replies and recorded votes included — into the
///     slice's tables, so a fresh data volume comes up with the discussions the running app
///     already shows. The orchestrated database keeps its data across restarts and this step runs
///     on every start, so a run that finds comments already there must leave them exactly as they
///     are instead of adding a second copy: repeated runs must not make the population grow or
///     change.
/// </summary>
public static class FindingCommentsSeed
{
    public static Task SeedAsync(
        FindingCommentsDbContext context,
        IReadOnlyList<Comment> comments,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
