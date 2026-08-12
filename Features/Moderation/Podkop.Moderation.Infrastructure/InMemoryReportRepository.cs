using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Infrastructure;

public sealed class InMemoryReportRepository(IEnumerable<Report> reports) : IReportRepository
{
    private readonly List<Report> _reports = reports.ToList();

    public Task<Report?> GetByReporterAndFindingAsync(string reporter, Guid findingId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_reports.FirstOrDefault(report =>
            report.Reporter == reporter && report.FindingId == findingId));

    public Task AddAsync(Report report, CancellationToken cancellationToken)
    {
        _reports.Add(report);
        return Task.CompletedTask;
    }
}
