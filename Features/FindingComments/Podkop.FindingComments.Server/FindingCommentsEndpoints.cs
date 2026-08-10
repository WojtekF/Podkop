using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Server;

/// <summary>The wire shape of PUT my-vote: direction is "up" or "down" (issue #13's API contract).</summary>
public sealed record SetCommentVoteRequest(string? Direction);

/// <summary>
///     The wire shape of POST comments (issue #17): the comment text plus, for a reply, the id
///     of the top-level comment it answers. A top-level comment sends no parent id.
/// </summary>
public sealed record PostCommentRequest(string? Text, Guid? ParentCommentId);

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

        group.MapPost("/", async (Guid findingId, PostCommentRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new PostComment(findingId, request.Text, request.ParentCommentId), cancellationToken);
                return ToPostResponse(findingId, result);
            })
            .WithName("PostFindingComment");

        var voteGroup = routes.MapGroup("/api/comments/{commentId:guid}/my-vote");

        voteGroup.MapPut("/", async (Guid commentId, SetCommentVoteRequest request, ISender sender,
                CancellationToken cancellationToken) =>
            {
                var direction = ParseDirection(request.Direction);
                if (direction is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "direction must be 'up' or 'down'.");
                }
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

    /// <summary>
    ///     Every error answer is a ProblemDetails whose <c>type</c> is a stable
    ///     <c>podkop:problem:&lt;slug&gt;</c> discriminator — several outcomes share a status
    ///     code, and clients dispatch on the type rather than parsing the prose detail.
    /// </summary>
    private static IResult ToPostResponse(Guid findingId, PostCommentResponse result) => result.Outcome switch
    {
        PostCommentOutcome.Posted => Results.Created($"/api/findings/{findingId}/comments", result.Comment),
        PostCommentOutcome.UnknownFinding => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            type: "podkop:problem:unknown-finding", detail: "No finding has that id."),
        PostCommentOutcome.UnknownParent => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            type: "podkop:problem:unknown-parent", detail: "No comment has that parent id."),
        PostCommentOutcome.ParentIsAReply => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:parent-is-a-reply",
            detail: "A reply cannot be replied to — threads are one level deep."),
        PostCommentOutcome.EmptyText => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:comment-empty", detail: "A comment needs text."),
        PostCommentOutcome.TextTooLong => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            type: "podkop:problem:comment-too-long",
            detail: $"A comment is at most {Comment.MaxTextLength} characters."),
        _ => throw new UnreachableException($"Unmapped post outcome '{result.Outcome}'."),
    };

    private static IResult ToVoteResponse(CommentVoteResult result) => result.Error switch
    {
        CommentVoteError.UnknownComment => Results.NotFound(),
        CommentVoteError.OwnComment => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            detail: "You cannot vote on your own comment."),
        _ => Results.Ok(result.Votes),
    };
}
