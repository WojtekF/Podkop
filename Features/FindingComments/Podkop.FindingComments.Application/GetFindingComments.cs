using MediatR;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Query for the whole discussion under one finding, used by the finding detail page.
///     Yields <c>null</c> when no finding has that id so the endpoint can answer 404, and an
///     empty list for a finding whose discussion is empty. Top-level comments come best-first
///     — net score descending, ties oldest-first — and each carries its replies in
///     chronological order. No paging yet (TODO.md).
/// </summary>
public sealed record GetFindingComments(Guid FindingId) : IRequest<IReadOnlyList<CommentThread>?>;

public sealed record CommentThread(
    Guid Id,
    string Author,
    string Text,
    int UpvoteCount,
    int DownvoteCount,
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
    DateTimeOffset CreatedAt);

public sealed class GetFindingCommentsHandler(
    ICommentRepository commentsRepository,
    IFindingLookup findingLookup)
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
            .GroupBy(comment => comment.ParentCommentId);

        return commentThreads
            .OrderByDescending(comment => comment.NetScore)
            .ThenBy(comment => comment.CreatedAt)
            .Select(comment => ToCommentThread(comment, commentRepliesByParent)
            ).ToList();
    }

    private static CommentThread ToCommentThread(Comment comment, IEnumerable<IGrouping<Guid?, Comment>> groupings)
    {
        var grouping = groupings
            .SingleOrDefault(kv => kv.Key == comment.Id);
        return new CommentThread(comment.Id, comment.Author, comment.Text, comment.UpvoteCount, comment.DownvoteCount,
            comment.CreatedAt,
            grouping is not null
                ? grouping.Select(reply =>
                        new CommentReply(
                            reply.Id,
                            reply.Author,
                            reply.Text,
                            reply.UpvoteCount,
                            reply.DownvoteCount,
                            reply.CreatedAt))
                    .OrderBy(cr => cr.CreatedAt)
                    .ToList()
                : Enumerable.Empty<CommentReply>().ToList());
    }
}