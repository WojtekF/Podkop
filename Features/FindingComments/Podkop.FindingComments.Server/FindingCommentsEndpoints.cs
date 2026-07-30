using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Server;

/// <summary>The wire shape of PUT my-vote: direction is "up" or "down" (issue #13's API contract).</summary>
public sealed record SetCommentVoteRequest(string? Direction);

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

        var voteGroup = routes.MapGroup("/api/comments/{commentId:guid}/my-vote");

        voteGroup.MapPut("/", async (Guid commentId, SetCommentVoteRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var direction = ParseDirection(request.Direction);
                if (direction is null) return Results.BadRequest();
                var result = await sender.Send(new SetCommentVote(commentId, direction.Value), cancellationToken);
                return ToVoteResponse(result);
            })
            .WithName("SetCommentVote");

        voteGroup.MapDelete("/", async (Guid commentId, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new WithdrawCommentVote(commentId), cancellationToken);
                return ToVoteResponse(result);
            })
            .WithName("WithdrawCommentVote");

        return routes;
    }

    private static VoteDirection? ParseDirection(string? direction) => direction switch
    {
        "up" => VoteDirection.Up,
        "down" => VoteDirection.Down,
        _ => null,
    };

    private static IResult ToVoteResponse(CommentVoteResult result) => result.Error switch
    {
        CommentVoteError.UnknownComment => Results.NotFound(),
        CommentVoteError.OwnComment => Results.BadRequest(),
        _ => Results.Ok(result.Votes),
    };
}
