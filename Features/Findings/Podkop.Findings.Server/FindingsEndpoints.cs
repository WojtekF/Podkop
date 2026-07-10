using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Findings.Application;

namespace Podkop.Findings.Server;

public static class FindingsEndpoints
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    public static IEndpointRouteBuilder MapFindings(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/findings");

        group.MapGet("/", async (ISender sender, string? feed, string? cursor, int? limit, CancellationToken cancellationToken) =>
            {
                if (feed != "main")
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "Only the 'main' feed is available.");
                }

                var pageSize = limit ?? DefaultLimit;
                if (pageSize is < 1 or > MaxLimit)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: $"limit must be between 1 and {MaxLimit}.");
                }

                try
                {
                    var page = await sender.Send(new GetMainPageFeed(cursor, pageSize), cancellationToken);
                    return Results.Ok(page);
                }
                catch (InvalidFeedCursorException)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "cursor is not valid.");
                }
            })
            .WithName("GetFindingsFeed");

        return routes;
    }
}
