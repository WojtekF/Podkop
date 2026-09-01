using Podkop.Tags.Application;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IUnitOfWork" /> (issue #96's pattern): committing flushes
///     the request's <see cref="TagsDbContext" /> — the same scoped instance the repository and
///     the inbox work through, so exactly what this delivery changed in the index, and its memory
///     of having changed it, turn durable together in one commit.
/// </summary>
public sealed class EfUnitOfWork(TagsDbContext context) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
