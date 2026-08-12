using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Command behind <c>POST /api/findings/{findingId}/my-report</c> (issue #32): files the
///     current user's report on the finding, citing one reportable Statute Point of the current
///     Statute and optionally carrying a short note. The reporter is the current user from the
///     <see cref="ICurrentUser" /> seam, never the request. The stored report pins the cited
///     point id and the Statute version in force at the filing instant (ADR 0006), read from the
///     injected clock. One report per user per finding — a duplicate is refused — and the
///     finding's author cannot report it. Filing changes no score, vote, or promotion state
///     (ADR 0008); the endpoint maps each refusal to a status code and problem type.
/// </summary>
public sealed record FileReport(Guid FindingId, Guid StatutePointId, string? Note)
    : IRequest<FileReportOutcome>;

public sealed class FileReportHandler(
    IReportRepository reportsRepository,
    IReportTargetLookup targetLookup,
    IStatuteLookup statuteLookup,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<FileReport, FileReportOutcome>
{
    public async Task<FileReportOutcome> Handle(FileReport request, CancellationToken cancellationToken)
    {
        var reportTarget =
            await targetLookup.GetAsync(ReportTargetKind.Finding, request.FindingId, cancellationToken);
        if (reportTarget is null) return FileReportOutcome.UnknownTarget;

        var report = await reportsRepository.GetByReporterAndTargetAsync(
            currentUser.UserName, ReportTargetKind.Finding, request.FindingId, cancellationToken);
        if (report is not null) return FileReportOutcome.AlreadyReported;

        var statute = await statuteLookup.GetCurrentAsync(cancellationToken);
        if (statute is null || !statute.ReportablePointIds.Contains(request.StatutePointId))
            return FileReportOutcome.NotReportablePoint;

        var fileReportResult = Report.File(
            Guid.CreateVersion7(),
            currentUser.UserName,
            reportTarget.Author,
            ReportTargetKind.Finding,
            reportTarget.Id,
            request.StatutePointId,
            statute.Version,
            request.Note,
            timeProvider.GetUtcNow());

        if (fileReportResult.Outcome == FileReportOutcome.Filed)
            await reportsRepository.AddAsync(fileReportResult!.Report, cancellationToken);

        return fileReportResult.Outcome;
    }
}
