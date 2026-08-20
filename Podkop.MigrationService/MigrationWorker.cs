using Microsoft.EntityFrameworkCore;

namespace Podkop.MigrationService;

/// <summary>
///     The one-shot worker that gates the API (issue #87, ADR 0010): applies every registered
///     participant's pending migrations in sequence, each wrapped in that context's execution
///     strategy; then — in Development only — invokes every participant's sample seeder,
///     idempotently, so a reused data volume is never double-seeded; then stops its own host so
///     the orchestrated resource completes and the API's startup gate opens. The registry is
///     empty until the first slice converts — both steps must still run and complete trivially
///     over it. The smoke suite in <c>Podkop.AppHost.Tests</c> defines done.
/// </summary>
public sealed class MigrationWorker(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    IHostApplicationLifetime hostApplicationLifetime,
    IEnumerable<SliceMigrationParticipant> participants) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var participant in participants)
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var context = participant.ResolveContext(scope.ServiceProvider);
                var executionStrategy = context.Database.CreateExecutionStrategy();
                await executionStrategy.ExecuteAsync(async () => await context.Database.MigrateAsync(stoppingToken));
            }

            if (hostEnvironment.IsDevelopment())
                foreach (var participant in participants)
                {
                    await using var scope = serviceProvider.CreateAsyncScope();
                    await participant.SeedAsync(scope.ServiceProvider, stoppingToken);
                }
        }
        catch
        {
            Environment.ExitCode = -1;
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }
}
