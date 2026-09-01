using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Tags.Application;

namespace Podkop.Tags.Server;

public static class TagsEndpoints
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    public static IEndpointRouteBuilder MapTags(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tags");

        group.MapGet("/{name}", async (
                string name,
                ISender sender,
                string? type,
                string? page,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var filter = ParseFilter(type);
                if (filter is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "type must be 'all', 'findings', or 'entries'.");
                }

                // page binds as a string: int binding failures throw in Development
                // (ThrowOnBadRequest) and would surface as 500 instead of the contract's 400.
                var pageNumber = 1;
                if (page is not null &&
                    (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber) ||
                     pageNumber < 1))
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

                // The name reaches the handler exactly as the URL spelled it: folding it to the
                // canonical tag is the query's business, and a name that folds to no tag that
                // exists comes back as null — the same 404 an unknown tag answers.
                var result = await sender.Send(
                    new GetTagPage(name, filter.Value, pageNumber, pageSize), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetTagPage");

        return routes;
    }

    private static TagContentFilter? ParseFilter(string? type) => type switch
    {
        null or "all" => TagContentFilter.All,
        "findings" => TagContentFilter.Findings,
        "entries" => TagContentFilter.Entries,
        _ => null,
    };
}
