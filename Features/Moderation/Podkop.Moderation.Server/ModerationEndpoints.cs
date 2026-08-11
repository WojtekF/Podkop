using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Server;

/// <summary>
///     The wire shape of POST my-report (issue #32's API contract): the id of the reportable
///     Statute Point the report cites, plus an optional short note.
/// </summary>
public sealed record FileReportRequest(Guid? StatutePointId, string? Note);

public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModeration(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/findings/{findingId:guid}/my-report");

        group.MapGet("/", async (Guid findingId, ISender sender, CancellationToken cancellationToken) =>
            {
                var status = await sender.Send(new GetMyReport(findingId), cancellationToken);
                return status is null ? Results.NotFound() : Results.Ok(status);
            })
            .WithName("GetMyFindingReport");

        group.MapPost("/", async (Guid findingId, FileReportRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                // A missing point id is a malformed request, rejected at the wire; every
                // semantic rule about the point (reportable, part of the current version)
                // is the handler's to enforce.
                if (request.StatutePointId is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        type: "podkop:problem:report-point-required",
                        detail: "A report must cite a Statute Point.");
                }

                var outcome = await sender.Send(
                    new FileReport(findingId, request.StatutePointId.Value, request.Note), cancellationToken);
                return ToFileResponse(findingId, outcome);
            })
            .WithName("FileFindingReport");

        return routes;
    }

    /// <summary>
    ///     Every error answer is a ProblemDetails whose <c>type</c> is a stable
    ///     <c>podkop:problem:&lt;slug&gt;</c> discriminator — several outcomes share a status
    ///     code, and clients dispatch on the type rather than parsing the prose detail.
    /// </summary>
    private static IResult ToFileResponse(Guid findingId, FileReportOutcome outcome) => outcome switch
    {
        FileReportOutcome.Filed => Results.Created($"/api/findings/{findingId}/my-report",
            new MyReportStatus(true)),
        FileReportOutcome.UnknownFinding => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            type: "podkop:problem:unknown-finding", detail: "No finding has that id."),
        FileReportOutcome.OwnFinding => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:own-finding", detail: "You cannot report your own finding."),
        FileReportOutcome.NotReportablePoint => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:point-not-reportable",
            detail: "The cited point is not a reportable point of the current Statute."),
        FileReportOutcome.AlreadyReported => Results.Problem(statusCode: StatusCodes.Status409Conflict,
            type: "podkop:problem:already-reported", detail: "You already reported this finding."),
        FileReportOutcome.NoteTooLong => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:report-note-too-long",
            detail: $"A report note is at most {Report.MaxNoteLength} characters."),
        _ => throw new UnreachableException($"Unmapped file-report outcome '{outcome}'."),
    };
}
