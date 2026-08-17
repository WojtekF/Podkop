using Podkop.Moderation.Domain;
using Podkop.Shared.Infrastructure;

namespace Podkop.Moderation.Infrastructure;

/// <summary>
///     A piece of reportable sample content as this generator needs to see it: its kind, its id,
///     and its author — handed in by the composition root's seed coordination, since this slice
///     never sees the content slices' own types (ADR 0003).
/// </summary>
public sealed record SampleReportTarget(ReportTargetKind Kind, Guid Id, string Author);

/// <summary>
///     One seeded Statute version a sample report may pin: its number and the ids of its
///     reportable points (ADR 0006) — projected from the seeded statute by the composition
///     root's seed coordination.
/// </summary>
public sealed record SampleCitableVersion(int Version, IReadOnlyList<Guid> ReportablePointIds);

/// <summary>
///     Development seed for the pending reports until PostgreSQL persistence lands (issue #34),
///     so the moderator case queue has cases to show without hand-filing reports first. The
///     generated reports must be coherent with the rest of the sample app — every one could
///     have been filed: its reporter is an author drawn from the given targets' author pool,
///     never the reported target's own author, and no reporter reports the same target twice;
///     it cites a reportable point of the version it pins, drawn from the given citable
///     versions. Coverage the queue makes observable: both target kinds are reported; at least
///     one target carries several reports, so grouping shows; at least one report pins a
///     superseded (non-latest) citable version, so the pinned-wording display shows; notes are
///     both present (within <see cref="Report.MaxNoteLength" />) and absent; and every FiledAt
///     is a distinct past instant, spread out so the oldest-grievance-first order is stable
///     and visible.
/// </summary>
public static class SampleReports
{
    public static IReadOnlyList<Report> GenerateFor(
        IReadOnlyList<SampleReportTarget> targets,
        IReadOnlyList<SampleCitableVersion> citableVersions)
    {
        var authorPool = targets.Select(target => target.Author).Distinct().ToList();
        var citable = citableVersions.Where(version => version.ReportablePointIds.Count > 0).ToList();
        if (citable.Count == 0) return [];

        var latestVersion = citable.OrderByDescending(version => version.Version).First();
        // A world with a single citable version has nothing superseded to pin, so the
        // designated old-citation report falls back to the latest.
        var supersededVersion = citable
            .Where(version => version.Version < latestVersion.Version)
            .MaxBy(version => version.Version) ?? latestVersion;

        var reports = new List<Report>();
        var newestInstant = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var (target, targetIndex) in PickReportedTargets(targets, authorPool).Select(
                     (target, index) => (target, index)))
        {
            // The first picked target carries a pile-up so grouping shows in the queue; every
            // other target carries a single report.
            var reporters = authorPool
                .Where(author => author != target.Author)
                .OrderBy(_ => Random.Shared.Next())
                .Take(targetIndex == 0 ? 3 : 1);

            foreach (var reporter in reporters)
            {
                var counter = reports.Count;
                var pinned = counter == 1 ? supersededVersion : latestVersion;
                reports.Add(new Report(
                    Guid.CreateVersion7(),
                    reporter,
                    target.Kind,
                    target.Id,
                    pinned.ReportablePointIds[Random.Shared.Next(pinned.ReportablePointIds.Count)],
                    pinned.Version,
                    counter % 2 == 0 ? SampleData.Lines[Random.Shared.Next(SampleData.Lines.Length)] : null,
                    // The spacing always beats the jitter, so the instants stay distinct and
                    // strictly older report by report.
                    newestInstant.AddHours(-(counter * 7 + Random.Shared.Next(0, 6)))));
            }
        }

        return reports;
    }

    /// <summary>
    ///     Which targets get reported: a few of each kind (skipping any target the author pool
    ///     cannot legally report), so both kinds show in the queue while most sample content
    ///     stays unreported.
    /// </summary>
    private static List<SampleReportTarget> PickReportedTargets(
        IReadOnlyList<SampleReportTarget> targets,
        IReadOnlyList<string> authorPool)
    {
        var reportable = targets
            .Where(target => authorPool.Any(author => author != target.Author))
            .ToList();

        List<SampleReportTarget> PickOfKind(ReportTargetKind kind, int count) =>
        [
            .. reportable
                .Where(target => target.Kind == kind)
                .OrderBy(_ => Random.Shared.Next())
                .Take(count)
        ];

        return [.. PickOfKind(ReportTargetKind.Finding, 3), .. PickOfKind(ReportTargetKind.Comment, 2)];
    }
}
