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
///         legitimately name. Specified by <c>TaggedContentRemovedConsumptionTests</c>.
///     </para>
/// </summary>
public sealed class TaggedContentRemovedHandler(
    ITagMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    IInbox inbox)
    : INotificationHandler<TaggedContentRemoved>
{
    public Task Handle(TaggedContentRemoved notification, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
