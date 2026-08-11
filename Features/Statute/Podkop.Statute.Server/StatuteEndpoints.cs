using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.Statute.Application;

namespace Podkop.Statute.Server;

public static class StatuteEndpoints
{
    public static IEndpointRouteBuilder MapDocuments(this IEndpointRouteBuilder routes)
    {
        var statute = routes.MapGroup("/api/statute");

        statute.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var current = await sender.Send(new GetCurrentStatute(), cancellationToken);
                return current is null ? Results.NotFound() : Results.Ok(current);
            })
            .WithName("GetCurrentStatute");

        statute.MapGet("/versions/{version:int}", async (int version, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var found = await sender.Send(new GetStatuteVersion(version), cancellationToken);
                return found is null ? Results.NotFound() : Results.Ok(found);
            })
            .WithName("GetStatuteVersion");

        var privacyPolicy = routes.MapGroup("/api/privacy-policy");

        privacyPolicy.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var current = await sender.Send(new GetCurrentPrivacyPolicy(), cancellationToken);
                return current is null ? Results.NotFound() : Results.Ok(current);
            })
            .WithName("GetCurrentPrivacyPolicy");

        privacyPolicy.MapGet("/versions/{version:int}", async (int version, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var found = await sender.Send(new GetPrivacyPolicyVersion(version), cancellationToken);
                return found is null ? Results.NotFound() : Results.Ok(found);
            })
            .WithName("GetPrivacyPolicyVersion");

        return routes;
    }
}
