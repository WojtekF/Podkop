using Podkop.FindingComments.Infrastructure;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.Server;

/// <summary>
///     The heartbeat of outbox delivery (issue #94, ADR 0014): for as long as the API host runs,
///     announcements the slices have committed keep becoming published events, one processor
///     pass at a time. This lives in the composition root because it is the one place that sees
///     every slice: today the FindingComments outbox is the only one to drain, and each converted
///     slice adds its context here rather than growing a loop of its own.
///     <para>
///         What it must do: pace itself by the configured poll interval — that interval is the
///         promised bound on how stale a cross-slice read can be — and give every pass a service
///         scope of its own, resolving the slice's context and the processor from it, because
///         consumers run inside the pass and expect the scoped world a request would give them.
///         A pass that fails — the database restarting mid-poll, for instance — must not take
///         the service down with it: delivery's whole promise is that it outlives crashes, so
///         the loop logs and keeps beating until the host itself stops it.
///     </para>
///     Not yet registered with the host: the cutover — wiring this service, the interceptor,
///     and the death of the legacy publish path — is one deliberate flip, described in
///     <see cref="DependencyInjection.AddFindingCommentsPersistence" />.
/// </summary>
public sealed class OutboxProcessingService(
    IServiceScopeFactory scopeFactory,
    OutboxProcessorOptions options,
    ILogger<OutboxProcessingService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        throw new NotImplementedException();
}
