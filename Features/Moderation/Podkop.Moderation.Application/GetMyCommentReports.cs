using MediatR;

namespace Podkop.Moderation.Application;

/// <summary>
///     Query behind <c>GET /api/findings/{findingId}/comments/my-reports</c> (issue #33): the ids
///     of the comments in this finding's discussion — top-level and replies alike — that the
///     current user already reported, so the detail page can show every comment's
///     already-reported state from its first render without one request per comment. Yields
///     <c>null</c> when no finding has that id so the endpoint can answer 404; a discussion with
///     nothing reported yields an empty list. Reports stay invisible to regular users — only the
///     current user's own reports are named, and only by target comment id.
/// </summary>
public sealed record GetMyCommentReports(Guid FindingId) : IRequest<MyCommentReportsStatus?>;

/// <summary>The comments of one finding's discussion the current user already reported.</summary>
public sealed record MyCommentReportsStatus(IReadOnlyList<Guid> ReportedCommentIds);

public sealed class GetMyCommentReportsHandler(
    IReportRepository reportsRepository,
    IFindingCommentsLookup commentsLookup,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyCommentReports, MyCommentReportsStatus?>
{
    public Task<MyCommentReportsStatus?> Handle(GetMyCommentReports request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
