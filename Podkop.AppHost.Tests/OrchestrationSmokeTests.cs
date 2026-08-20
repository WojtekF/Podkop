using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Podkop.AppHost.Tests;

/// <summary>
///     Serializes the orchestration suites: each boots the whole AppHost — persistent postgres
///     container included — so two running concurrently would contend for the same container.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OrchestrationTestsCollection
{
    public const string Name = "orchestration";
}

/// <summary>
///     Boots the full AppHost orchestration once for the whole suite (a distributed-app start is
///     far too heavy per test) and records the outcomes the tests assert on. Waits are attempted
///     only for resources the application model actually declares, so a still-unimplemented graph
///     fails fast instead of burning the whole startup window. The green path needs Docker —
///     ADR 0010 makes it a hard requirement for dev machines and CI — and the window is sized for
///     a first-run postgres image pull. A startup failure (no container runtime, say) is recorded
///     rather than thrown, so the model-shape test still reports on the graph while the runtime
///     tests surface the real reason.
/// </summary>
public sealed class OrchestrationSmokeFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupWindow = TimeSpan.FromMinutes(5);

    private readonly List<(string Resource, string? State, int? ExitCode)> _events = [];

    private readonly TaskCompletionSource<(string? State, int? ExitCode)> _migrationsTerminal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DistributedApplication? _app;

    public DistributedApplicationModel Model { get; private set; } = null!;

    /// <summary>Ordered resource-state observations, frozen before the tests run.</summary>
    public IReadOnlyList<(string Resource, string? State, int? ExitCode)> Events => _events;

    public bool PostgresBecameHealthy { get; private set; }

    /// <summary>The "migrations" resource's first terminal snapshot; null if it never got there.</summary>
    public (string? State, int? ExitCode)? MigrationsTerminal { get; private set; }

    /// <summary>
    ///     Status of GET /health on the API, probed only after the worker completed successfully;
    ///     null if the probe never ran or the API never answered.
    /// </summary>
    public HttpStatusCode? ApiHealthAfterMigrations { get; private set; }

    /// <summary>Why StartAsync failed (e.g. no container runtime), if it did; null when it started.</summary>
    public Exception? StartupFailure { get; private set; }

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Podkop_AppHost>();
        _app = await builder.BuildAsync();
        Model = _app.Services.GetRequiredService<DistributedApplicationModel>();

        var notifications = _app.ResourceNotifications;
        using var window = new CancellationTokenSource(StartupWindow);
        var watchTask = WatchEventsAsync(notifications, window.Token);

        try
        {
            await _app.StartAsync(window.Token);
        }
        catch (Exception exception)
        {
            // E.g. no container runtime. Recorded rather than rethrown so the model-shape test
            // still runs; the runtime tests assert on this and surface the real reason.
            StartupFailure = exception;
        }

        if (StartupFailure is null && Model.Resources.Any(r => r.Name == "postgres"))
        {
            try
            {
                // The watcher above (subscribed before StartAsync) covers instantly-exiting
                // resources; postgres health can only flip after startup, so waiting from here
                // misses nothing.
                await notifications.WaitForResourceHealthyAsync("postgres", window.Token);
                PostgresBecameHealthy = true;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (StartupFailure is null && Model.Resources.Any(r => r.Name == "migrations"))
        {
            try
            {
                MigrationsTerminal = await _migrationsTerminal.Task.WaitAsync(window.Token);
                if (MigrationsTerminal?.ExitCode == 0)
                {
                    await ProbeApiHealthAsync(window.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Freeze the event log before any test reads it — the watcher must not append while
        // assertions enumerate.
        window.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private async Task WatchEventsAsync(ResourceNotificationService notifications, CancellationToken cancellationToken)
    {
        await foreach (var resourceEvent in notifications.WatchAsync(cancellationToken))
        {
            var state = resourceEvent.Snapshot.State?.Text;
            lock (_events)
            {
                _events.Add((resourceEvent.Resource.Name, state, resourceEvent.Snapshot.ExitCode));
            }

            if (resourceEvent.Resource.Name == "migrations" && KnownResourceStates.TerminalStates.Contains(state))
            {
                _migrationsTerminal.TrySetResult((state, resourceEvent.Snapshot.ExitCode));
            }
        }
    }

    private async Task ProbeApiHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Retry until the gated API comes up: completion of the worker opens the gate, but the
            // server still needs a moment to boot before it can answer.
            using var client = _app!.CreateHttpClient("server", "http");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await client.GetAsync("/health", cancellationToken);
                    ApiHealthAfterMigrations = response.StatusCode;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // Not listening yet — stays null unless a later attempt answers.
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
            // No allocated endpoint to probe — recorded as "never answered".
        }
    }
}

/// <summary>
///     Solution-level orchestration smoke suite for issue #87: running the AppHost must bring up
///     PostgreSQL (data volume, persistent container lifetime, pgAdmin) with the
///     <c>podkopdb</c> database, run the <c>Podkop.MigrationService</c> worker to successful
///     completion, and hold the API back until that gate opens. Red until the orchestration graph
///     in <c>AppHost.cs</c> and the worker's logic are implemented.
/// </summary>
[Collection(OrchestrationTestsCollection.Name)]
public sealed class OrchestrationSmokeTests(OrchestrationSmokeFixture fixture)
    : IClassFixture<OrchestrationSmokeFixture>
{
    [Fact]
    public void Postgres_is_orchestrated_with_data_volume_persistent_lifetime_pgadmin_and_the_podkopdb_database()
    {
        var postgres = Assert.Single(fixture.Model.Resources, r => r.Name == "postgres");

        Assert.Contains(postgres.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Type == ContainerMountType.Volume);
        Assert.Contains(postgres.Annotations.OfType<ContainerLifetimeAnnotation>(),
            lifetime => lifetime.Lifetime == ContainerLifetime.Persistent);
        Assert.Contains(fixture.Model.Resources,
            r => r.Name.Contains("pgadmin", StringComparison.OrdinalIgnoreCase));

        var database = Assert.Single(fixture.Model.Resources, r => r.Name == "podkopdb");
        var podkopDb = Assert.IsType<PostgresDatabaseResource>(database);
        Assert.Same(postgres, podkopDb.Parent);
    }

    [Fact]
    public void Postgres_reports_healthy()
    {
        Assert.True(
            fixture.StartupFailure is null,
            $"The orchestration failed to start: {fixture.StartupFailure?.Message}");
        Assert.True(
            fixture.PostgresBecameHealthy,
            "The 'postgres' resource never reported healthy within the startup window — "
            + "is a PostgreSQL server orchestrated, and is Docker running?");
    }

    [Fact]
    public void The_migration_worker_runs_to_completion_and_stops_itself()
    {
        Assert.True(
            fixture.StartupFailure is null,
            $"The orchestration failed to start: {fixture.StartupFailure?.Message}");
        Assert.True(
            fixture.MigrationsTerminal is not null,
            "The 'migrations' resource never reached a terminal state within the startup window — "
            + "is the worker orchestrated against the database, and does it stop its own host once "
            + "its migrate and seed steps finish?");
        Assert.Equal(0, fixture.MigrationsTerminal!.Value.ExitCode);
    }

    [Fact]
    public void The_api_answers_requests_only_after_the_migration_worker_completes()
    {
        Assert.True(
            fixture.StartupFailure is null,
            $"The orchestration failed to start: {fixture.StartupFailure?.Message}");

        var migrationsCompletedAt = FirstIndex(e =>
            e.Resource == "migrations" && KnownResourceStates.TerminalStates.Contains(e.State));
        var serverRunningAt = FirstIndex(e =>
            e.Resource == "server" && e.State == KnownResourceStates.Running);

        Assert.True(
            migrationsCompletedAt >= 0,
            "The 'migrations' resource never completed, so the API's startup gate cannot have "
            + "been exercised.");
        Assert.True(
            serverRunningAt >= 0,
            "The 'server' resource never reached Running after the migration worker completed.");
        Assert.True(
            migrationsCompletedAt < serverRunningAt,
            "The API started serving before the migration worker completed — 'server' must be "
            + "held back until 'migrations' has run to completion.");

        Assert.True(
            fixture.ApiHealthAfterMigrations is not null,
            "The API never answered GET /health after the migration worker completed.");
        Assert.Equal(HttpStatusCode.OK, fixture.ApiHealthAfterMigrations);
    }

    private int FirstIndex(Func<(string Resource, string? State, int? ExitCode), bool> match)
    {
        for (var i = 0; i < fixture.Events.Count; i++)
        {
            if (match(fixture.Events[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
