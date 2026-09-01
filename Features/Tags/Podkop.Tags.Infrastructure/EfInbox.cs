using Microsoft.EntityFrameworkCore;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Application;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IInbox" /> (ADR 0014): consumed announcements are rows in
///     this slice's own schema, tracked through the request's <see cref="TagsDbContext" /> — the
///     same scoped instance the repository works through — so recording one turns durable in the
///     very commit that makes the announcement's effect durable, which is the whole idempotency
///     guarantee.
/// </summary>
public sealed class EfInbox(TagsDbContext context, TimeProvider timeProvider) : IInbox
{
    public Task<bool> AlreadyConsumedAsync(Guid eventId, CancellationToken cancellationToken) =>
        context.InboxMessages.AnyAsync(m => m.Id == eventId, cancellationToken);

    public Task RecordConsumedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        context.InboxMessages.Add(new InboxMessage(eventId, timeProvider.GetUtcNow()));
        return Task.CompletedTask;
    }
}
