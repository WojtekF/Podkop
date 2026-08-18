using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Query behind <c>GET /api/findings/{findingId}/my-report</c> (issue #32): whether the
///     current user already reported the finding, so the detail page can show the
///     already-reported state from its first render. Yields <c>null</c> when no finding has that
///     id so the endpoint can answer 404. Reports themselves stay invisible to regular users —
///     the only member-visible fact is "did I already report this finding", and only for the
///     current user's own report. Only a PENDING report counts (issue #35): once a Verdict
///     resolves the user's report — read against <see cref="IVerdictRepository" /> — the
///     answer returns to not-reported and the user may report the finding afresh.
/// </summary>
public sealed record GetMyReport(Guid FindingId) : IRequest<MyReportStatus?>;

/// <summary>The one member-visible fact about a finding's reports: whether the current user filed one.</summary>
public sealed record MyReportStatus(bool Reported);

public sealed class GetMyReportHandler(
    IReportRepository reportsRepository,
    IVerdictRepository verdictsRepository,
    IReportTargetLookup targetLookup,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyReport, MyReportStatus?>
{
    public async Task<MyReportStatus?> Handle(GetMyReport request, CancellationToken cancellationToken)
    {
        var reportTarget =
            await targetLookup.GetAsync(ReportTargetKind.Finding, request.FindingId, cancellationToken);
        if (reportTarget is null) return null;

        var report = await reportsRepository.GetByReporterAndTargetAsync(
            currentUser.UserName, ReportTargetKind.Finding, request.FindingId, cancellationToken);

        var verdicts =
            await verdictsRepository.GetByTargetAsync(ReportTargetKind.Finding, request.FindingId, cancellationToken);

        return new MyReportStatus(report is not null &&
                                  verdicts.All(verdict => !verdict.ResolvedReportIds.Contains(report.Id)));
    }
}
