using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

public interface IReportRepository
{
    /// <summary>
    ///     Every report the reporter filed against the target, resolved ones included — the
    ///     lookup behind both the one-PENDING-report-per-user-per-target rule and the my-report
    ///     state (issues #32/#33, pending-scoped by issue #35). Reports are immutable, so a
    ///     reporter accumulates one per judged-and-refiled cycle on the same target; the
    ///     handlers derive against the verdicts which of them, if any, is still pending.
    ///     Never reported yields none.
    /// </summary>
    Task<IReadOnlyList<Report>> GetByReporterAndTargetAsync(
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
    ///     Every stored report, resolved ones included — reports are immutable and never leave
    ///     the store. Pending-ness is not this store's fact (issue #35): Application handlers
    ///     derive it against the verdicts, a report being pending iff no Verdict's
    ///     ResolvedReportIds references its id.
    /// </summary>
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Every report ever filed against one target, resolved ones included — the dismissal's
    ///     input (issue #35): the handler derives which are still pending against the target's
    ///     verdicts and resolves exactly those. A never-reported target yields none.
    /// </summary>
    Task<IReadOnlyList<Report>> GetByTargetAsync(
        ReportTargetKind targetKind, Guid targetId, CancellationToken cancellationToken);

    Task AddAsync(Report report, CancellationToken cancellationToken);
}
