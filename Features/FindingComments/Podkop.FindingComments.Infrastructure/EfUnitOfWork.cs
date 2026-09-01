using Podkop.FindingComments.Application;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IUnitOfWork" /> (issue #68, patterned on issue #96):
///     committing flushes the request's <see cref="FindingCommentsDbContext" /> — the same scoped
///     instance the repository loads and adds through, so exactly what the use case mutated is
///     what turns durable, in one commit.
/// </summary>
public sealed class EfUnitOfWork(FindingCommentsDbContext context) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
