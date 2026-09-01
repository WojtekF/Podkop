using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Server;

/// <summary>
///     The wire shape of PUT my-vote (issue #15's API contract): <c>type</c> is "dig" or "bury";
///     a bury also carries a <c>reason</c> (one of the five bury reasons). A dig ignores reason.
/// </summary>
public sealed record SetFindingVoteRequest(string? Type, string? Reason);

public static class FindingsEndpoints
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    /// <summary>The most findings one batch-by-ids call may hydrate — one full page's worth.</summary>
    private const int MaxBatchSize = MaxLimit;

    public static IEndpointRouteBuilder MapFindings(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/findings");

        group.MapGet("/", async (ISender sender, string? feed, string? page, int? limit, CancellationToken cancellationToken) =>
            {
                if (feed != "main")
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "Only the 'main' feed is available.");
                }

                // page binds as a string: int binding failures throw in Development
                // (ThrowOnBadRequest) and would surface as 500 instead of the contract's 400.
                var pageNumber = 1;
                if (page is not null &&
                    (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber) || pageNumber < 1))
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "page must be a positive integer.");
                }

                var pageSize = limit ?? DefaultLimit;
                if (pageSize is < 1 or > MaxLimit)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: $"limit must be between 1 and {MaxLimit}.");
                }

                var result = await sender.Send(new GetMainPageFeed(pageNumber, pageSize), cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetFindingsFeed");

        // Declared before the /{id:guid} route only for reading order — the guid constraint means
        // "batch" could never have matched it anyway.
        group.MapGet("/batch", async (ISender sender, string? ids, CancellationToken cancellationToken) =>
            {
                var requested = ParseIds(ids);
                if (requested is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: $"ids must be 1 to {MaxBatchSize} comma-separated finding ids.");
                }

                var findings = await sender.Send(new GetFindingsByIds(requested), cancellationToken);
                return Results.Ok(findings);
            })
            .WithName("GetFindingsByIds");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var detail = await sender.Send(new GetFindingDetail(id), cancellationToken);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            })
            .WithName("GetFindingById");

        var voteGroup = routes.MapGroup("/api/findings/{id:guid}/my-vote");

        voteGroup.MapPut("/", async (Guid id, SetFindingVoteRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var side = ParseSide(request.Type);
                if (side is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "type must be 'dig' or 'bury'.");
                }
                // A missing or unrecognised reason is passed through as null; the "a bury needs a
                // reason" rule is the domain's to enforce (issue #15), so the endpoint does not
                // pre-reject it here.
                var reason = ParseReason(request.Reason);
                var result = await sender.Send(new SetFindingVote(id, side.Value, reason), cancellationToken);
                return ToVoteResponse(result);
            })
            .WithName("SetFindingVote");

        voteGroup.MapDelete("/", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new WithdrawFindingVote(id), cancellationToken);
                return ToVoteResponse(result);
            })
            .WithName("WithdrawFindingVote");

        return routes;
    }

    /// <summary>
    ///     The ids a batch request names, or <c>null</c> when it names none, names something that
    ///     is not an id, or names more than one page's worth (issue #77). The cap matches the
    ///     largest page a caller can ask any feed for, because hydrating one page is what this
    ///     serves.
    /// </summary>
    private static IReadOnlyList<Guid>? ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return null;

        var parsed = new List<Guid>();
        foreach (var candidate in ids.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(candidate, out var id)) return null;
            parsed.Add(id);
        }

        return parsed.Count is 0 or > MaxBatchSize ? null : parsed;
    }

    private static FindingVoteSide? ParseSide(string? type) => type switch
    {
        "dig" => FindingVoteSide.Dig,
        "bury" => FindingVoteSide.Bury,
        _ => null,
    };

    private static BuryReason? ParseReason(string? reason) => reason switch
    {
        "duplicate" => BuryReason.Duplicate,
        "spam" => BuryReason.Spam,
        "false-information" => BuryReason.FalseInformation,
        "inappropriate-content" => BuryReason.InappropriateContent,
        "unsuitable" => BuryReason.Unsuitable,
        _ => null,
    };

    private static IResult ToVoteResponse(FindingVoteResult result) => result.Error switch
    {
        FindingVoteError.UnknownFinding => Results.NotFound(),
        FindingVoteError.OwnFinding => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            detail: "You cannot vote on your own finding."),
        FindingVoteError.BuryReasonRequired => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            detail: "A bury must carry a reason."),
        _ => Results.Ok(result.Votes),
    };
}
