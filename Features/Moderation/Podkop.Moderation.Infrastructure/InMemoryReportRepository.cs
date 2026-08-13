using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Infrastructure;

public sealed class InMemoryReportRepository(IEnumerable<Report> reports) : IReportRepository
{
    private readonly List<Report> _reports = reports.ToList();

    public Task<Report?> GetByReporterAndTargetAsync(string reporter, ReportTargetKind targetKind, Guid targetId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_reports.FirstOrDefault(report =>
            report.Reporter == reporter && report.TargetKind == targetKind && report.TargetId == targetId));

    public Task<IReadOnlyList<Report>> GetByReporterAndTargetsAsync(string reporter, ReportTargetKind targetKind,
        IReadOnlyList<Guid> targetIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Report>>(_reports
            .Where(report => report.Reporter == reporter && report.TargetKind == targetKind &&
                             targetIds.Contains(report.TargetId))
            .ToList());

    public Task AddAsync(Report report, CancellationToken cancellationToken)
    {
        _reports.Add(report);
        return Task.CompletedTask;
    }
}
