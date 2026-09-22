using MediatR;
using Podkop.Tags.Contracts;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Application;

/// <summary>
///     Consumes a content slice's <see cref="TaggedContentAnnounced" /> and brings the membership
///     index in line with the announced tag set (ADR 0011). The announcement carries the whole
///     set, so this is a replacement rather than an addition: after it, the content is filed
///     under exactly the announced tags and under no others — which is what makes an edit that
///     drops a tag actually drop it from that tag's page. An announcement naming a content type
///     this slice does not index is not indexed.
///     <para>
///         Delivery is at-least-once (ADR 0014), so the work must be idempotent: an announcement
///         this slice's <see cref="IInbox" /> already holds changes nothing, and one it does not
///         is acted on and recorded in the same commit, so a redelivery can never re-file content
///         a later announcement has already moved. Specified by
///         <c>TaggedContentConsumptionTests</c>.
///     </para>
/// </summary>
public sealed class TaggedContentAnnouncedHandler(
    ITagMembershipRepository memberships,
    IUnitOfWork unitOfWork,
    IInbox inbox)
    : INotificationHandler<TaggedContentAnnounced>
{
    public async Task Handle(TaggedContentAnnounced notification, CancellationToken cancellationToken)
    {
        if (await inbox.AlreadyConsumedAsync(notification.EventId, cancellationToken)) return;

        var taggedContentType = TaggedContentTypeExtensions.FromApiString(notification.ContentType);
        if (!taggedContentType.HasValue) return;

        var normalizedIncomingTags = notification.Tags
            .Select(Tag.TryFold)
            .Where(tag => tag != null)
            .Distinct()
            .Cast<Tag>()
            .ToList();

        var alreadyExistingTags =
            await memberships.GetForContentAsync(taggedContentType.Value, notification.ContentId, cancellationToken);

        var removedTags = alreadyExistingTags
            .Where(tag => normalizedIncomingTags.All(normalizedTag => normalizedTag.Name != tag.Tag))
            .ToList();

        var newTags = normalizedIncomingTags
            .Where(tag => alreadyExistingTags.All(existingTag => existingTag.Tag != tag.Name))
            .Select(tag =>
                new TagMembership(tag.Name, taggedContentType.Value, notification.ContentId, notification.CreatedAt));

        foreach (var tag in newTags) memberships.Add(tag);

        memberships.RemoveRange(removedTags);

        await inbox.RecordConsumedAsync(notification.EventId, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
