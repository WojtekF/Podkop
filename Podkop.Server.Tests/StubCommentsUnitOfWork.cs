using Podkop.FindingComments.Application;

namespace Podkop.Server.Tests;

/// <summary>
///     The FindingComments slice's commit seam, doubled next to
///     <see cref="StubCommentRepository" /> (issue #68): the stubbed comments live in memory,
///     where mutations on held aggregates are visible in place, so committing has nothing left to
///     make durable. Registered wherever the stub repository is — a suite that doubles the store
///     must double its commit seam too, or a use case's commit would reach for the real context
///     no host configured.
/// </summary>
internal sealed class StubCommentsUnitOfWork : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
