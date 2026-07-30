using MediatR;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Query for the whole discussion under one finding, used by the finding detail page.
///     Yields <c>null</c> when no finding has that id so the endpoint can answer 404, and an
///     empty list for a finding whose discussion is empty. Top-level comments come best-first
///     — net score descending, ties oldest-first — and each carries its replies in
///     chronological order. No paging yet (TODO.md). Every row also carries the current
///     user's vote — <c>"up"</c>, <c>"down"</c>, or <c>null</c> — so highlighting survives
///     a page reload (issue #18).
/// </summary>
public sealed record GetFindingComments(Guid FindingId) : IRequest<IReadOnlyList<CommentThread>?>;

public sealed record CommentThread(
    Guid Id,
    string Author,
    string Text,
    int UpvoteCount,
    int DownvoteCount,
    string? MyVote,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CommentReply> Replies);

/// <summary>
///     A reply row deliberately carries no replies of its own: the DTO shape itself states
///     that threads are exactly one level deep.
/// </summary>
public sealed record CommentReply(
    Guid Id,
    string Author,
    string Text,
    int UpvoteCount,
    int DownvoteCount,
    string? MyVote,
    DateTimeOffset CreatedAt);

public sealed class GetFindingCommentsHandler(
    ICommentRepository commentsRepository,
    IFindingLookup findingLookup,
    ICurrentUser currentUser)
    : IRequestHandler<GetFindingComments, IReadOnlyList<CommentThread>?>
{
    public async Task<IReadOnlyList<CommentThread>?> Handle(GetFindingComments request,
        CancellationToken cancellationToken)
    {
        if (!await findingLookup.ExistsAsync(request.FindingId, cancellationToken)) return null;

        var commentsFromFinding = await commentsRepository.GetByFindingIdAsync(request.FindingId, cancellationToken);
        var commentThreads = commentsFromFinding.Where(comment => !comment.IsReply);
        var commentRepliesByParent = commentsFromFinding
            .Where(comment => comment.IsReply)
            .ToLookup(comment => comment.ParentCommentId!.Value);

        return commentThreads
            .OrderByDescending(comment => comment.NetScore)
            .ThenBy(comment => comment.CreatedAt)
            .Select(comment => ToCommentThread(comment, commentRepliesByParent, currentUser.UserName)
            ).ToList();
    }

    private static CommentThread ToCommentThread(Comment comment, ILookup<Guid, Comment> repliesByParent,
        string currentUser)
    {
        return new CommentThread(comment.Id, comment.Author, comment.Text, comment.UpvoteCount, comment.DownvoteCount,
            comment.VoteBy(currentUser),
            comment.CreatedAt,
            repliesByParent[comment.Id]
                .OrderBy(cr => cr.CreatedAt)
                .Select(reply =>
                    new CommentReply(
                        reply.Id,
                        reply.Author,
                        reply.Text,
                        reply.UpvoteCount,
                        reply.DownvoteCount,
                        reply.VoteBy(currentUser),
                        reply.CreatedAt))
                .ToList());
    }
}