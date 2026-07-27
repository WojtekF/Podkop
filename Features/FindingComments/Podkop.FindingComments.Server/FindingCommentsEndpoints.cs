using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.FindingComments.Application;

namespace Podkop.FindingComments.Server;

public static class FindingCommentsEndpoints
{
    public static IEndpointRouteBuilder MapFindingComments(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/findings/{findingId:guid}/comments");

        group.MapGet("/", async (Guid findingId, ISender sender, CancellationToken cancellationToken) =>
            {
                var threads = await sender.Send(new GetFindingComments(findingId), cancellationToken);
                return threads is null ? Results.NotFound() : Results.Ok(threads);
            })
            .WithName("GetFindingComments");

        return routes;
    }
}
