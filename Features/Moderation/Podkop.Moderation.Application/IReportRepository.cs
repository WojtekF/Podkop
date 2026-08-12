using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

public interface IReportRepository
{
    /// <summary>
    ///     The reporter's report on the finding, if they filed one — the lookup behind both the
    ///     one-report-per-user-per-finding rule and the my-report state (issue #32).
    /// </summary>
    Task<Report?> GetByReporterAndFindingAsync(string reporter, Guid findingId, CancellationToken cancellationToken);

    Task AddAsync(Report report, CancellationToken cancellationToken);
}
