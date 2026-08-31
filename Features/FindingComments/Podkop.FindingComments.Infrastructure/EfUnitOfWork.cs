using MediatR;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IUnitOfWork" /> (issue #68, patterned on issue #96):
///     committing flushes the request's <see cref="FindingCommentsDbContext" /> — the same scoped
///     instance the repository loads and adds through, so exactly what the use case mutated is
///     what turns durable, in one commit.
/// </summary>
public sealed class EfUnitOfWork(FindingCommentsDbContext context, IPublisher publisher) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);

        var comments = context.ChangeTracker.Entries<Comment>().Select(e => e.Entity);

        foreach (var comment in comments)
        {
            // The legacy delivery path stamps its own identity so the running app stays coherent
            // until the outbox owns delivery (issue #94); this whole publish loop dies at the
            // cutover, when committing becomes nothing but the save.
            foreach (var added in comment.DomainEvents.OfType<CommentAdded>())
                await publisher.Publish(
                    new CommentPosted(Guid.CreateVersion7(), added.CommentId, added.FindingId),
                    cancellationToken);
            comment.ClearDomainEvents();
        }
    }
}
