using Podkop.Findings.Application;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IUnitOfWork" /> (issue #96): committing flushes the
///     request's <see cref="FindingsDbContext" /> — the same scoped instance the repository loads
///     through, so exactly what the use case mutated on its tracked aggregates is what turns
///     durable, in one commit.
/// </summary>
public sealed class EfUnitOfWork(FindingsDbContext context) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
