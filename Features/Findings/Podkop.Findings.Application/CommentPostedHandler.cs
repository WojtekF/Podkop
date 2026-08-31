using MediatR;
using Podkop.FindingComments.Contracts;

namespace Podkop.Findings.Application;

/// <summary>
///     Consumes the FindingComments slice's <see cref="CommentPosted" /> contract event (ADR
///     0003, issue #17) and counts the new comment on the finding — the cross-slice sync that
///     keeps the comment count truthful everywhere it appears. This slice references only the
///     producer's Contracts project; the count is eventually consistent by design.
///     <para>
///         Delivery is at-least-once once the outbox owns it (issue #94, ADR 0014), so counting
///         must be idempotent: an announcement this slice's <see cref="IInbox" /> already holds
///         changes nothing, and one it does not is acted on and recorded in the same commit —
///         even when the finding it names no longer exists, so a redelivery is never waiting to
///         count a comment on a finding that reappears. Specified by
///         <c>CommentPostedConsumptionTests</c>.
///     </para>
/// </summary>
public sealed class CommentPostedHandler(
    IFindingRepository findingsRepository,
    IUnitOfWork unitOfWork,
    IInbox inbox)
    : INotificationHandler<CommentPosted>
{
    public async Task Handle(CommentPosted notification, CancellationToken cancellationToken)
    {
        var finding = await findingsRepository.GetByIdAsync(notification.FindingId, cancellationToken);
        finding?.IncrementCommentCount();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
