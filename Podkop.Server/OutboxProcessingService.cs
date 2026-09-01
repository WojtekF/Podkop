using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.Server;

/// <summary>
///     The heartbeat of outbox delivery (issue #94, ADR 0014): for as long as the API host runs,
///     announcements the slices have committed keep becoming published events, one processor
///     pass at a time. This lives in the composition root because it is the one place that sees
///     every slice: FindingComments announces its posted comments and — since issue #77 — Findings
///     announces its tag sets, and each producing slice adds its context here rather than growing
///     a loop of its own.
///     <para>
///         What it must do: pace itself by the configured poll interval — that interval is the
///         promised bound on how stale a cross-slice read can be — and give every pass a service
///         scope of its own, resolving the slice's context and the processor from it, because
///         consumers run inside the pass and expect the scoped world a request would give them.
///         A pass that fails — the database restarting mid-poll, for instance — must not take
///         the service down with it: delivery's whole promise is that it outlives crashes, so
///         the loop logs and keeps beating until the host itself stops it.
///     </para>
///     The API host registers this as its one hosted delivery loop, alongside the registry,
///     publisher, and options it resolves; the worker runs no loop — it only writes. The write
///     side it drains is described in
///     <see cref="DependencyInjection.AddFindingCommentsPersistence" />.
/// </summary>
public sealed class OutboxProcessingService(
    IServiceScopeFactory scopeFactory,
    OutboxProcessorOptions options,
    ILogger<OutboxProcessingService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox delivery service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Outbox delivery pass starting");
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                logger.LogDebug("Scope created");

                var registry = scope.ServiceProvider.GetRequiredService<ContractEventTypeRegistry>();
                var publisher = scope.ServiceProvider.GetRequiredService<IContractEventPublisher>();
                var timeProviderForProcessor = scope.ServiceProvider.GetRequiredService<TimeProvider>();
                logger.LogDebug("Processor dependencies resolved");

                var processor = new OutboxProcessor(registry, publisher, timeProviderForProcessor, options);

                // One pass drains every producing slice's outbox in turn. Sequential on purpose:
                // the pass is the delivery loop's unit of pacing, and the catch below owns what
                // a failing drain costs the rest.
                foreach (var outbox in ProducingOutboxes(scope.ServiceProvider))
                    await processor.ProcessPendingAsync(outbox, stoppingToken);

                logger.LogInformation("Outbox delivery pass finished");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Outbox delivery pass failed; retrying in {PollInterval}", options.PollInterval);
            }

            await Task.Delay(options.PollInterval, timeProvider, stoppingToken);
        }

        logger.LogInformation("Outbox delivery service stopped");
    }

    /// <summary>
    ///     The outboxes one pass drains, in the order it drains them. Every slice that announces
    ///     anything appears here — the one list that has to grow when a slice starts producing,
    ///     and the only place in the system that knows they are more than one.
    /// </summary>
    private static IEnumerable<DbContext> ProducingOutboxes(IServiceProvider scopedServices) =>
    [
        scopedServices.GetRequiredService<FindingCommentsDbContext>(),
        scopedServices.GetRequiredService<FindingsDbContext>(),
    ];
}
