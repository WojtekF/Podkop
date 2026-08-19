using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Query behind <c>GET /api/moderation/cases</c> (issue #34): the queue of open Cases a
///     moderator judges from — every reported piece of content grouped with all its pending
///     reports. Judging starts with the Dismiss verdict (issue #35); further actions arrive
///     with later tickets (issue #36 on).
/// </summary>
public sealed record GetCaseQueue : IRequest<CaseQueueResult>;

/// <summary>
///     How the queue query answered: the queue was listed, or the acting user holds no
///     moderator role and is refused — the queue is a moderators-only surface (CONTEXT.md).
/// </summary>
public enum CaseQueueOutcome
{
    Listed,
    NotModerator
}

/// <summary>The queue answer: the outcome, and the ordered cases when it was listed.</summary>
public sealed record CaseQueueResult(CaseQueueOutcome Outcome, IReadOnlyList<CaseSummary>? Cases);

/// <summary>
///     One open Case as the queue shows it (issue #34, CONTEXT.md): a reported finding or
///     comment with all its pending reports. A Case has no identity of its own — the question
///     issue #34 deferred is resolved (issue #35): it stays the derived grouping key
///     (TargetKind + TargetId) of the target's pending reports, existing iff at least one
///     report is pending, and a Verdict references the target and the resolved report ids,
///     never a case id. TargetKind carries the <c>ReportTargetKind</c> name ("Finding" /
///     "Comment") across the wire; FindingId names the finding page where the content lives —
///     the finding itself, or the finding a reported comment belongs to. Reporter identities
///     never leave the slice: the case carries their count and their reports' contents only.
/// </summary>
public sealed record CaseSummary(
    string TargetKind,
    Guid TargetId,
    Guid FindingId,
    string Preview,
    string Author,
    int ReportCount,
    IReadOnlyList<CaseReportSummary> Reports)
{
    /// <summary>
    ///     The most preview text one case carries (issue #34); longer source text — a comment
    ///     can run to thousands of characters — is cut to this.
    /// </summary>
    public const int MaxPreviewLength = 200;
}

/// <summary>
///     One pending report of a case (issue #34): the cited Statute Point resolved against the
///     version the report pinned (ADR 0006) — PointCitation composed <c>section.point</c>
///     (e.g. "2.1"), the same form the statute page renders — its optional note, and when it
///     was filed. Deliberately no reporter.
/// </summary>
public sealed record CaseReportSummary(
    string PointCitation,
    string PointText,
    string? Note,
    DateTimeOffset FiledAt);

/// <summary>
///     Answers the case queue (issue #34). The contract the specs pin down:
///     the acting user must hold the Moderator role — anyone else is refused with
///     <see cref="CaseQueueOutcome.NotModerator" /> — and role is a fact of the Users slice,
///     read through this slice's own <see cref="IModeratorLookup" /> port. Only PENDING
///     reports feed the queue (issue #35) — a report is pending iff no Verdict's
///     ResolvedReportIds references its id, the verdicts read through
///     <see cref="IVerdictRepository" />: resolved reports vanish from their case, and a
///     target whose reports are all resolved has no case at all. Pending reports group one
///     case per reported content
///     (target kind + id): cases order oldest grievance first — ascending by the earliest
///     report's FiledAt, ties broken by ascending target id — and a case's reports order
///     ascending by FiledAt. Content facts (author, preview source text, owning finding) come
///     through <see cref="ICaseContentLookup" />; the preview is cut to
///     <see cref="CaseSummary.MaxPreviewLength" /> characters. Cases about the acting
///     moderator's own content stay listed — the never-on-their-own-content rule bites when
///     judging (issue #35), not viewing. Each report's cited point resolves through
///     <see cref="IStatuteLookup" /> against the version the report pinned. Nothing removes
///     content or amends filed reports yet, so a lookup answering null for a stored report's
///     target or pinned point is a broken invariant, not a case to present.
/// </summary>
public sealed class GetCaseQueueHandler(
    IReportRepository reportsRepository,
    IVerdictRepository verdictsRepository,
    ICurrentUser currentUser,
    IModeratorLookup moderatorLookup,
    ICaseContentLookup caseContentLookup,
    IStatuteLookup statuteLookup)
    : IRequestHandler<GetCaseQueue, CaseQueueResult>
{
    public async Task<CaseQueueResult> Handle(GetCaseQueue request, CancellationToken cancellationToken)
    {
        if (!await moderatorLookup.IsModeratorAsync(currentUser.UserName, cancellationToken))
            return new CaseQueueResult(CaseQueueOutcome.NotModerator, null);

        var verdicts = await verdictsRepository.GetAllAsync(cancellationToken);

        var reports = await reportsRepository.GetAllAsync(cancellationToken);
        var groupedReports = reports
            .Where(report =>
                report.IsPendingAgainst(verdicts))
            .OrderBy(report => report.FiledAt)
            .ThenBy(report => report.TargetId)
            .GroupBy(report => (Kind: report.TargetKind, Id: report.TargetId));

        var caseSummaries = await MapGroupedReportsToCaseSummary(groupedReports, cancellationToken);
        return new CaseQueueResult(
            CaseQueueOutcome.Listed,
            caseSummaries
                .Where(c => c != null)
                .Cast<CaseSummary>()
                .ToList());
    }

    private async ValueTask<IEnumerable<CaseSummary?>> MapGroupedReportsToCaseSummary(
        IEnumerable<IGrouping<(ReportTargetKind Kind, Guid Id), Report>> groupedReports,
        CancellationToken cancellationToken) =>
        await groupedReports
            .ToAsyncEnumerable()
            .Select(MapGroupedReportsToCase)
            .ToListAsync(cancellationToken);

    private async ValueTask<CaseSummary?> MapGroupedReportsToCase(
        IGrouping<(ReportTargetKind Kind, Guid Id ), Report> group, CancellationToken cancellationToken)
    {
        var sortedReport = group
            .OrderBy(value => value.FiledAt)
            .ThenByDescending(value => value.Id)
            .ToList();

        var @case = await caseContentLookup.GetAsync(
            group.Key.Kind,
            group.Key.Id,
            cancellationToken);

        return @case is null
            ? null
            : new CaseSummary(
                group.Key.Kind.ToString(),
                group.Key.Id,
                @case.FindingId,
                @case.Preview.Length > CaseSummary.MaxPreviewLength
                    ? @case.Preview.Substring(0, CaseSummary.MaxPreviewLength)
                    : @case.Preview,
                @case.Author,
                sortedReport.Count(),
                await sortedReport
                    .ToAsyncEnumerable()
                    .Select(MapReportToCaseReportSummary)
                    .ToListAsync(cancellationToken));
    }

    private async ValueTask<CaseReportSummary> MapReportToCaseReportSummary(Report report,
        CancellationToken cancellationToken)
    {
        var statutePoint = await statuteLookup.GetPointAsync(
            report.StatutePointId,
            report.StatuteVersion,
            cancellationToken);

        return new CaseReportSummary(
            $"{statutePoint!.SectionNumber}.{statutePoint.PointNumber}",
            statutePoint.Text,
            report.Note,
            report.FiledAt
        );
    }
}
