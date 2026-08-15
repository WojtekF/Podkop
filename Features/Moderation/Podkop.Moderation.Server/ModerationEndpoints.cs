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

        return routes;
    }

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
