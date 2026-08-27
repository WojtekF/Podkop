using MediatR;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     Command behind <c>POST /api/findings/{findingId}/comments</c> (issue #17): posts a
///     top-level comment (<see cref="ParentCommentId" /> null) or a reply (set to a top-level
///     comment's id — a reply's parent can never itself be a reply, threads are one level deep).
///     The author is the current user from the <see cref="ICurrentUser" /> seam, never the
///     request. Text is trimmed before validation and storage.
/// </summary>
public sealed record PostComment(Guid FindingId, string? Text, Guid? ParentCommentId)
    : IRequest<PostCommentResponse>;

/// <summary>
///     Outcome of a post, in the domain's own <see cref="PostCommentOutcome" /> vocabulary:
///     <see cref="PostCommentOutcome.Posted" /> carries the created comment in the same shape a
///     GET row has (<see cref="CommentReply" />), so the frontend renders it straight from the
///     response — no refetch; every other outcome carries no comment and the endpoint maps it
///     to a status code and problem type.
/// </summary>
public sealed record PostCommentResponse(PostCommentOutcome Outcome, CommentReply? Comment);

public sealed class PostCommentHandler(
    ICommentRepository commentsRepository,
    IFindingLookup findingLookup,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PostComment, PostCommentResponse>
{
    public async Task<PostCommentResponse> Handle(PostComment request, CancellationToken cancellationToken)
    {
        if (!await findingLookup.ExistsAsync(request.FindingId, cancellationToken))
            return new PostCommentResponse(PostCommentOutcome.UnknownFinding, null);

        Comment? parentComment = null;
        if (request.ParentCommentId is not null)
        {
            parentComment = await commentsRepository.GetByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment is null || parentComment.FindingId != request.FindingId)
                return new PostCommentResponse(PostCommentOutcome.UnknownParent, null);
        }

        var postCommentResult = Comment.Post(
            id: Guid.CreateVersion7(),
            request.FindingId,
            parentComment,
            author: currentUser.UserName,
            request.Text,
            createdAt: DateTimeOffset.UtcNow);

        if (postCommentResult.Outcome != PostCommentOutcome.Posted)
            return new PostCommentResponse(postCommentResult.Outcome, null);

        await commentsRepository.AddAsync(postCommentResult.Comment!, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return new PostCommentResponse(PostCommentOutcome.Posted,
            postCommentResult.Comment!.ToCommentReply(currentUser.UserName));
    }
}
