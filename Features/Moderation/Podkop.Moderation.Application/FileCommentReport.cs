using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Command behind <c>POST /api/comments/{commentId}/my-report</c> (issue #33): files the
///     current user's report on one comment — top-level or reply — under exactly the
///     finding-report rules. The reporter is the current user from the <see cref="ICurrentUser" />
///     seam, never the request. The stored report pins the cited point id and the Statute version
///     in force at the filing instant (ADR 0006), read from the injected clock. One report per
///     user per comment — a duplicate is refused — and the comment's author cannot report it.
///     A cited point must be a reportable point of the current Statute. Filing changes no score,
///     vote, or promotion state (ADR 0008); the endpoint maps each refusal to a status code and
///     problem type.
/// </summary>
public sealed record FileCommentReport(Guid CommentId, Guid StatutePointId, string? Note)
    : IRequest<FileReportOutcome>;

public sealed class FileCommentReportHandler(
    IReportRepository reportsRepository,
    IReportTargetLookup targetLookup,
    IStatuteLookup statuteLookup,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<FileCommentReport, FileReportOutcome>
{
    public Task<FileReportOutcome> Handle(FileCommentReport request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
