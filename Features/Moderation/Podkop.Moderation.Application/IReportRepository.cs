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
    ///     Every report the reporter filed against targets of one kind — the lookup behind the
    ///     batch my-reports state a finding's discussion loads with (issue #33).
    /// </summary>
    Task<IReadOnlyList<Report>> GetByReporterAndKindAsync(
        string reporter, ReportTargetKind targetKind, CancellationToken cancellationToken);

    Task AddAsync(Report report, CancellationToken cancellationToken);
}
