using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Podkop.MigrationService.Tests;

/// <summary>
///     The seed half of the worker's failure contract (issue #87): a registered seeder that
///     throws must exit the worker with a non-zero code, because exit 0 is precisely what opens
///     the API's startup gate. The gate mechanics themselves (non-zero exit ⇒ the API is held
///     back) are already pinned end-to-end by <c>Podkop.AppHost.Tests</c>, so this test only
///     needs the exit-code half and can host the worker in-process — no orchestration, no
///     Docker, and no test seams in the worker's production wiring. The participant's context
///     runs on Sqlite purely to carry the migrate step's machinery with zero migrations; no
///     schema fidelity is involved, so ADR 0010's rejection of Sqlite for endpoint tests does
///     not apply here.
/// </summary>
public sealed class MigrationWorkerTests
{
    [Fact]
    public async Task A_failing_seeder_exits_the_worker_with_a_nonzero_code()
    {
        var seederInvoked = false;
        var participant = new SliceMigrationParticipant(
            "seed-fault",
            _ => new NoMigrationsContext(
                new DbContextOptionsBuilder<NoMigrationsContext>().UseSqlite("Data Source=:memory:").Options),
            (_, _) =>
            {
                seederInvoked = true;
                throw new InvalidOperationException("simulated seed-step failure");
            });

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Development, because that is the only environment whose runs include the seed step.
            EnvironmentName = Environments.Development
        });
        builder.Services.AddHostedService<MigrationWorker>();
        builder.Services.AddSingleton(participant);
        using var host = builder.Build();

        var originalExitCode = Environment.ExitCode;
        try
        {
            try
            {
                // A failing BackgroundService stops its host either way (the default StopHost
                // behavior); what separates failure from success is the exit code the process
                // would report — Environment.ExitCode, since Program.cs returns nothing.
                await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                Assert.Fail("The worker neither completed nor stopped its host within 30s of a failing seeder.");
            }

            Assert.True(
                seederInvoked,
                "The seed step never ran — the failure came from an earlier step, so this test "
                + "did not exercise the seed path.");
            Assert.True(
                Environment.ExitCode != 0,
                "The worker exited with code 0 despite a failing seeder — a success exit code "
                + "opens the API's startup gate on a run whose sample data is broken.");
        }
        finally
        {
            // Process-global; restore it so the test host's own exit code stays clean.
            Environment.ExitCode = originalExitCode;
        }
    }

    private sealed class NoMigrationsContext(DbContextOptions<NoMigrationsContext> options)
        : DbContext(options);
}
