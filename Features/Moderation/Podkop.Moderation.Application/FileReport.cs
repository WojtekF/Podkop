using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Command behind <c>POST /api/findings/{findingId}/my-report</c> (issue #32) and
///     <c>POST /api/comments/{commentId}/my-report</c> (issue #33): files the current user's
///     report on one piece of content — a finding, or a comment top-level or reply — under one
///     set of rules; the target kind is data, not a separate use case. The reporter is the
///     current user from the <see cref="ICurrentUser" /> seam, never the request. The stored
///     report pins the cited point id and the Statute version in force at the filing instant
///     (ADR 0006), read from the injected clock. One PENDING report per user per target
///     (issue #35): a duplicate is refused while the earlier report awaits judgment, but a
///     report a Verdict resolved blocks nothing — its reporter may report the target afresh,
///     the resolution read against <see cref="IVerdictRepository" />. Authors cannot report
///     their own content. A cited point must be a reportable
///     point of the current Statute. Filing changes no score, vote, or promotion state (ADR
///     0008); the endpoint maps each refusal to a status code and a kind-specific problem type.
/// </summary>
public sealed record FileReport(ReportTargetKind TargetKind, Guid TargetId, Guid StatutePointId, string? Note)
    : IRequest<FileReportOutcome>;

public sealed class FileReportHandler(
    IReportRepository reportsRepository,
    IVerdictRepository verdictsRepository,
    IReportTargetLookup targetLookup,
    IStatuteLookup statuteLookup,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<FileReport, FileReportOutcome>
{
    public async Task<FileReportOutcome> Handle(FileReport request, CancellationToken cancellationToken)
    {
        var reportTarget =
            await targetLookup.GetAsync(request.TargetKind, request.TargetId, cancellationToken);
        if (reportTarget is null) return FileReportOutcome.UnknownTarget;

        var previousReport = await reportsRepository.GetByReporterAndTargetAsync(
            currentUser.UserName, request.TargetKind, request.TargetId, cancellationToken);
        if (previousReport is not null) return FileReportOutcome.AlreadyReported;

        var statute = await statuteLookup.GetCurrentAsync(cancellationToken);
        if (statute is null || !statute.ReportablePointIds.Contains(request.StatutePointId))
            return FileReportOutcome.NotReportablePoint;

        var fileReportResult = Report.File(
            Guid.CreateVersion7(),
            currentUser.UserName,
            reportTarget.Author,
            request.TargetKind,
            reportTarget.Id,
            request.StatutePointId,
            statute.Version,
            request.Note,
            timeProvider.GetUtcNow());

        if (fileReportResult.Outcome == FileReportOutcome.Filed)
            await reportsRepository.AddAsync(fileReportResult.Report!, cancellationToken);

        return fileReportResult.Outcome;
    }
}
