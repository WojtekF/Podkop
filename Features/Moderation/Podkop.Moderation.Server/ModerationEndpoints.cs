using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Server;

/// <summary>
///     The wire shape of POST my-report on either target kind (issues #32/#33 API contract): the
///     id of the reportable Statute Point the report cites, plus an optional short note.
/// </summary>
public sealed record FileReportRequest(Guid? StatutePointId, string? Note);

/// <summary>
///     The wire shape of POST verdict (issue #35 API contract): the verdict kind by its exact
///     <c>VerdictKind</c> name. Only "Dismissed" is understood this ticket — "Upheld" joins the
///     wire vocabulary with issue #36.
/// </summary>
public sealed record VerdictRequest(string? Verdict);

public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModeration(this IEndpointRouteBuilder routes)
    {
        var findingGroup = routes.MapGroup("/api/findings/{findingId:guid}/my-report");

        findingGroup.MapGet("/", async (Guid findingId, ISender sender, CancellationToken cancellationToken) =>
            {
                var status = await sender.Send(new GetMyReport(findingId), cancellationToken);
                return status is null ? Results.NotFound() : Results.Ok(status);
            })
            .WithName("GetMyFindingReport");

        findingGroup.MapPost("/", async (Guid findingId, FileReportRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                // A missing point id is a malformed request, rejected at the wire; every
                // semantic rule about the point (reportable, part of the current version)
                // is the handler's to enforce.
                if (request.StatutePointId is null) return PointRequiredProblem();

                var outcome = await sender.Send(
                    new FileReport(ReportTargetKind.Finding, findingId, request.StatutePointId.Value, request.Note),
                    cancellationToken);
                return ToFileResponse(outcome, $"/api/findings/{findingId}/my-report", FindingProblems);
            })
            .WithName("FileFindingReport");

        routes.MapPost("/api/comments/{commentId:guid}/my-report", async (Guid commentId, FileReportRequest request,
                ISender sender, CancellationToken cancellationToken) =>
            {
                if (request.StatutePointId is null) return PointRequiredProblem();

                var outcome = await sender.Send(
                    new FileReport(ReportTargetKind.Comment, commentId, request.StatutePointId.Value, request.Note),
                    cancellationToken);
                return ToFileResponse(outcome, $"/api/comments/{commentId}/my-report", CommentProblems);
            })
            .WithName("FileCommentReport");

        routes.MapGet("/api/findings/{findingId:guid}/comments/my-reports",
                async (Guid findingId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var status = await sender.Send(new GetMyCommentReports(findingId), cancellationToken);
                    return status is null ? Results.NotFound() : Results.Ok(status);
                })
            .WithName("GetMyCommentReports");

        routes.MapGet("/api/moderation/cases", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetCaseQueue(), cancellationToken);
                return ToCaseQueueResponse(result);
            })
            .WithName("GetCaseQueue");

        routes.MapPost("/api/moderation/cases/{targetKind}/{targetId:guid}/verdict",
                async (string targetKind, Guid targetId, VerdictRequest request, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    // A kind outside the vocabulary is a nonexistent case, not a malformed
                    // request: the route names content kinds (never the enum's numerals), and
                    // unknown content answers 404. Names match case-insensitively; the verdict
                    // below does not.
                    var kindName = Enum.GetNames<ReportTargetKind>().SingleOrDefault(
                        name => string.Equals(name, targetKind, StringComparison.OrdinalIgnoreCase));
                    if (kindName is null) return UnknownCaseProblem();
                    var parsedKind = Enum.Parse<ReportTargetKind>(kindName);

                    if (string.IsNullOrWhiteSpace(request.Verdict)) return VerdictRequiredProblem();

                    // Exact, case-sensitive kind names: "dismissed" is as unknown as garbage,
                    // and "Upheld" stays unknown until issue #36 wires it.
                    if (request.Verdict != nameof(VerdictKind.Dismissed)) return UnknownVerdictProblem();

                    var result = await sender.Send(new DismissCase(parsedKind, targetId), cancellationToken);
                    return ToDismissResponse(result);
                })
            .WithName("IssueCaseVerdict");

        routes.MapGet("/api/moderation/log", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetModerationLog(), cancellationToken);
                return ToModerationLogResponse(result);
            })
            .WithName("GetModerationLog");

        return routes;
    }

    /// <summary>
    ///     Judging is a moderators-only act with its own conduct rule (issue #35): both
    ///     refusals are 403s told apart by type — a Member wholesale, a moderator only for a
    ///     case about their own content. A target without pending reports has no case, so
    ///     never-reported and already-dismissed answer the same 404.
    /// </summary>
    private static IResult ToDismissResponse(DismissCaseResult result) =>
        result.Outcome switch
        {
            DismissCaseOutcome.Dismissed => Results.NoContent(),
            DismissCaseOutcome.NotModerator => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                type: "podkop:problem:moderators-only",
                detail: "Only moderators may issue verdicts."),
            DismissCaseOutcome.OwnCase => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                type: "podkop:problem:own-case",
                detail: "Moderators never judge cases about their own content."),
            DismissCaseOutcome.UnknownCase => UnknownCaseProblem(),
            _ => throw new UnreachableException($"Unmapped dismiss-case outcome '{result.Outcome}'."),
        };

    /// <summary>
    ///     The log is a moderators-only surface like the queue (issue #35): a Member's call is
    ///     refused with the same stable <c>podkop:problem:moderators-only</c> discriminator.
    /// </summary>
    private static IResult ToModerationLogResponse(ModerationLogResult result) =>
        result.Outcome switch
        {
            ModerationLogOutcome.Listed => Results.Ok(result.Entries),
            ModerationLogOutcome.NotModerator => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                type: "podkop:problem:moderators-only",
                detail: "Only moderators may view the moderation log."),
            _ => throw new UnreachableException($"Unmapped moderation-log outcome '{result.Outcome}'."),
        };

    private static IResult UnknownCaseProblem() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound,
            type: "podkop:problem:unknown-case",
            detail: "No open case exists for this content.");

    private static IResult VerdictRequiredProblem() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:verdict-required",
            detail: "A verdict must be named.");

    private static IResult UnknownVerdictProblem() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:unknown-verdict",
            detail: "No verdict of that name exists.");

    /// <summary>
    ///     The queue is a moderators-only surface (issue #34): a Member's call is refused with
    ///     the stable <c>podkop:problem:moderators-only</c> discriminator — the area's existence
    ///     is no secret (the Statute names moderation), only its contents are.
    /// </summary>
    private static IResult ToCaseQueueResponse(CaseQueueResult result) =>
        result.Outcome switch
        {
            CaseQueueOutcome.Listed => Results.Ok(result.Cases),
            CaseQueueOutcome.NotModerator => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                type: "podkop:problem:moderators-only",
                detail: "Only moderators may view the case queue."),
            _ => throw new UnreachableException($"Unmapped case-queue outcome '{result.Outcome}'."),
        };

    /// <summary>
    ///     The target-kind-specific halves of the refusal vocabulary: the unknown-target and
    ///     own-content answers name the kind in their problem type and prose; every other
    ///     refusal reads the same whichever kind was reported.
    /// </summary>
    private sealed record TargetProblems(
        string UnknownType, string UnknownDetail, string OwnType, string OwnDetail, string AlreadyReportedDetail);

    private static readonly TargetProblems FindingProblems = new(
        "podkop:problem:unknown-finding", "No finding has that id.",
        "podkop:problem:own-finding", "You cannot report your own finding.",
        "You already reported this finding.");

    private static readonly TargetProblems CommentProblems = new(
        "podkop:problem:unknown-comment", "No comment has that id.",
        "podkop:problem:own-comment", "You cannot report your own comment.",
        "You already reported this comment.");

    private static IResult PointRequiredProblem() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:report-point-required",
            detail: "A report must cite a Statute Point.");

    /// <summary>
    ///     Every error answer is a ProblemDetails whose <c>type</c> is a stable
    ///     <c>podkop:problem:&lt;slug&gt;</c> discriminator — several outcomes share a status
    ///     code, and clients dispatch on the type rather than parsing the prose detail.
    /// </summary>
    private static IResult ToFileResponse(FileReportOutcome outcome, string location, TargetProblems problems) =>
        outcome switch
        {
            FileReportOutcome.Filed => Results.Created(location, new MyReportStatus(true)),
            FileReportOutcome.UnknownTarget => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                type: problems.UnknownType, detail: problems.UnknownDetail),
            FileReportOutcome.OwnContent => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                type: problems.OwnType, detail: problems.OwnDetail),
            FileReportOutcome.NotReportablePoint => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                type: "podkop:problem:point-not-reportable",
                detail: "The cited point is not a reportable point of the current Statute."),
            FileReportOutcome.AlreadyReported => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                type: "podkop:problem:already-reported", detail: problems.AlreadyReportedDetail),
            FileReportOutcome.NoteTooLong => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                type: "podkop:problem:report-note-too-long",
                detail: $"A report note is at most {Report.MaxNoteLength} characters."),
            _ => throw new UnreachableException($"Unmapped file-report outcome '{outcome}'."),
        };
}
