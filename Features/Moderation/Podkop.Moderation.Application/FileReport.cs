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
    public Task<FileReportOutcome> Handle(FileReport request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
