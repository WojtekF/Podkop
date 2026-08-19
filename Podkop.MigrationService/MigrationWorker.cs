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
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
