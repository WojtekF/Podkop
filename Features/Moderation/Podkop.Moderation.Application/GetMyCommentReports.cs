using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Query behind <c>GET /api/findings/{findingId}/comments/my-reports</c> (issue #33): the ids
///     of the comments in this finding's discussion — top-level and replies alike — that the
///     current user already reported, so the detail page can show every comment's
///     already-reported state from its first render without one request per comment. Yields
///     <c>null</c> when no finding has that id so the endpoint can answer 404; a discussion with
///     nothing reported yields an empty list. Reports stay invisible to regular users — only the
///     current user's own reports are named, and only by target comment id. Only PENDING
///     reports are named (issue #35): a comment report a Verdict resolved — read against
///     <see cref="IVerdictRepository" /> — drops out of the answer, and the user may report
///     that comment afresh.
/// </summary>
public sealed record GetMyCommentReports(Guid FindingId) : IRequest<MyCommentReportsStatus?>;

/// <summary>The comments of one finding's discussion the current user already reported.</summary>
public sealed record MyCommentReportsStatus(IReadOnlyList<Guid> ReportedCommentIds);

public sealed class GetMyCommentReportsHandler(
    IReportRepository reportsRepository,
    IVerdictRepository verdictsRepository,
    IFindingCommentsLookup commentsLookup,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyCommentReports, MyCommentReportsStatus?>
{
    public async Task<MyCommentReportsStatus?> Handle(GetMyCommentReports request, CancellationToken cancellationToken)
    {
        var comments = await commentsLookup.GetCommentIdsAsync(request.FindingId, cancellationToken);
        if (comments is null) return null;

        var existingReports = await reportsRepository.GetByReporterAndTargetsAsync(
            currentUser.UserName,
            ReportTargetKind.Comment,
            comments,
            cancellationToken);
        return new MyCommentReportsStatus(existingReports.Select(report => report.TargetId).ToList());
    }
}
