using Podkop.Moderation.Domain;

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
        IReadOnlyList<SampleCitableVersion> citableVersions) =>
        throw new NotImplementedException();
}
