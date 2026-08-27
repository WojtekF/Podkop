using MediatR;
using Podkop.FindingComments.Contracts;

namespace Podkop.Findings.Application;

/// <summary>
///     Consumes the FindingComments slice's <see cref="CommentPosted" /> contract event (ADR
///     0003, issue #17) and counts the new comment on the finding — the cross-slice sync that
///     keeps the comment count truthful everywhere it appears. This slice references only the
///     producer's Contracts project; the count is eventually consistent by design.
/// </summary>
public sealed class CommentPostedHandler(IFindingRepository findingsRepository, IUnitOfWork unitOfWork)
    : INotificationHandler<CommentPosted>
{
    public async Task Handle(CommentPosted notification, CancellationToken cancellationToken)
    {
        var finding = await findingsRepository.GetByIdAsync(notification.FindingId, cancellationToken);
        finding?.IncrementCommentCount();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
