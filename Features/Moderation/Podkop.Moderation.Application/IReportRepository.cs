using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

public interface IReportRepository
{
    /// <summary>
    ///     The reporter's report on the target, if they filed one — the lookup behind both the
    ///     one-report-per-user-per-target rule and the my-report state (issues #32/#33).
    /// </summary>
    Task<Report?> GetByReporterAndTargetAsync(
        string reporter, ReportTargetKind targetKind, Guid targetId, CancellationToken cancellationToken);

    /// <summary>
    ///     The reports the reporter filed against the named targets of one kind — the lookup
    ///     behind the batch my-reports state a finding's discussion loads with (issue #33).
    ///     Moderation stores no finding-to-comment relation (ADR 0003), so the caller names the
    ///     discussion's comments — from <see cref="IFindingCommentsLookup" /> — and the store
    ///     narrows to them rather than answering with every report the reporter ever filed.
    ///     Naming no targets yields no reports.
    /// </summary>
    Task<IReadOnlyList<Report>> GetByReporterAndTargetsAsync(
        string reporter, ReportTargetKind targetKind, IReadOnlyList<Guid> targetIds,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Every stored report — the case queue's whole feed (issue #34): no Verdict exists
    ///     until issue #35, so every report is pending by definition. That ticket narrows this
    ///     to the reports still awaiting judgment.
    /// </summary>
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(Report report, CancellationToken cancellationToken);
}
