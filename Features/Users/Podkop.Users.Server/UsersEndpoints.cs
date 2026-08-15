using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Users.Application;

namespace Podkop.Users.Server;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder routes)
    {
        // No null → 404 mapping: the seed guarantees the acting user a record, so the query
        // always answers (or throws on a broken invariant, surfacing as 500).
        routes.MapGet("/api/my-user", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetMyUser(), cancellationToken)))
            .WithName("GetMyUser");

        return routes;
    }
}
