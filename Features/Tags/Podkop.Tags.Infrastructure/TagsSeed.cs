using Podkop.Tags.Domain;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The Development-only database seed for the sample membership index (issue #77), invoked by
///     <c>Podkop.MigrationService</c> after this slice's migrations are applied — and after the
///     Findings seed, because every seeded membership names a finding the database must already
///     hold. It puts the given rows into the slice's tables, so a fresh data volume comes up with
///     tag pages that actually list the sample findings. The orchestrated database keeps its data
///     across restarts and this step runs on every start, so a run that finds the index already
///     populated must leave it exactly as it is instead of adding a second copy: repeated runs
///     must not make the index grow or change.
/// </summary>
public static class TagsSeed
{
    public static Task SeedAsync(
        TagsDbContext context,
        IReadOnlyList<TagMembership> memberships,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
