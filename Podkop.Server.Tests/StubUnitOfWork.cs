using Podkop.Findings.Application;

namespace Podkop.Server.Tests;

/// <summary>
///     The Findings slice's commit seam, doubled next to <see cref="StubFindingRepository" />
///     (issue #96): the stubbed findings live in memory, where mutations on held aggregates are
///     visible in place, so committing has nothing left to make durable. Registered wherever the
///     stub repository is — a suite that doubles the store must double its commit seam too, or a
///     use case's commit would reach for the real context no host configured.
/// </summary>
internal sealed class StubUnitOfWork : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
