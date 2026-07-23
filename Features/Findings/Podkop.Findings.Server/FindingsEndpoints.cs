using System.Globalization;
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

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var detail = await sender.Send(new GetFindingDetail(id), cancellationToken);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            })
            .WithName("GetFindingById");

        return routes;
    }
}
