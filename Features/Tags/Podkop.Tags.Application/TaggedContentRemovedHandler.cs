using MediatR;
using Podkop.Tags.Contracts;

namespace Podkop.Tags.Application;

/// <summary>
///     Consumes a content slice's <see cref="TaggedContentRemoved" /> and takes the vanished
///     content out of the index entirely — every tag it was filed under, not one of them (ADR
///     0011). This is the direction that lets the index shrink: without it, a tag whose last
///     content is gone would keep answering a page of references to nothing instead of returning
///     to 404.
///     <para>
///         Delivery is at-least-once (ADR 0014), so the work must be idempotent in the same way
///         its sibling's is — including for content this slice never indexed, which a removal may
///         legitimately name. Specified by <c>TaggedContentConsumptionTests</c>.
///     </para>
/// </summary>
public sealed class TaggedContentRemovedHandler(
    ITagMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    IInbox inbox)
    : INotificationHandler<TaggedContentRemoved>
{
    public async Task Handle(TaggedContentRemoved notification, CancellationToken cancellationToken)
    {
        if (await inbox.AlreadyConsumedAsync(notification.EventId, cancellationToken)) return;

        var taggedContentType = TaggedContentTypeExtensions.FromApiString(notification.ContentType);
        if (taggedContentType.HasValue)
        {
            var tagMemberships = await memberships.GetForContentAsync(
                taggedContentType.Value,
                notification.ContentId,
                cancellationToken);
            memberships.RemoveRange(tagMemberships);
        }

        await inbox.RecordConsumedAsync(notification.EventId, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
