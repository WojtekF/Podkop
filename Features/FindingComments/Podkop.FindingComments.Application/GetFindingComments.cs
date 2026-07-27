using MediatR;

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
    public Task<IReadOnlyList<CommentThread>?> Handle(GetFindingComments request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
