using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Application;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IInbox" /> (issue #94): consumed announcements are rows
///     in this slice's own schema, tracked through the request's
///     <see cref="FindingsDbContext" /> — the same scoped instance the repository loads through —
///     so recording one turns durable in the very commit that makes the announcement's effect
///     durable, which is the whole idempotency guarantee.
/// </summary>
public sealed class EfInbox(FindingsDbContext context, TimeProvider timeProvider) : IInbox
{
    public Task<bool> AlreadyConsumedAsync(Guid eventId, CancellationToken cancellationToken) =>
        context.InboxMessages.AnyAsync(m => m.Id == eventId, cancellationToken);

    public Task RecordConsumedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        context.InboxMessages.Add(new InboxMessage(eventId, timeProvider.GetUtcNow()));
        return Task.CompletedTask;
    }
}
