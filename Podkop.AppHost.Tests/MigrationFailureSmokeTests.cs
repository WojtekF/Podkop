using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Podkop.AppHost.Tests;

/// <summary>
///     Boots the AppHost with the worker's test-only fault hook armed
///     (<c>PODKOP_MIGRATIONS_FAULT</c>), so the injected participant fails during the migrate
///     step, and records how the run ended: the "migrations" resource's terminal snapshot, and
///     whether the gated "server" resource ever reached Running. Which failure state a gated
///     resource lands in is an Aspire implementation detail, so the gate assertion is a bounded
///     negative — after the worker's terminal state, "server" gets a grace period in which a
///     broken gate would demonstrably open — rather than a wait for one specific state.
/// </summary>
public sealed class MigrationFailureFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     How long after the worker's exit a wrongly-opened gate gets to manifest as a Running
    ///     server. A gate-opening bug shows within a few seconds (the earlier pre-fix runs did);
    ///     the margin keeps slow machines honest.
    /// </summary>
    private static readonly TimeSpan GateGracePeriod = TimeSpan.FromSeconds(15);

    private readonly TaskCompletionSource<(string? State, int? ExitCode)> _migrationsTerminal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<bool> _serverRunning =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DistributedApplication? _app;
    private string? _lastServerState;

    /// <summary>Why StartAsync failed (e.g. no container runtime), if it did; null when it started.</summary>
    public Exception? StartupFailure { get; private set; }

    /// <summary>The "migrations" resource's first terminal snapshot; null if it never got there.</summary>
    public (string? State, int? ExitCode)? MigrationsTerminal { get; private set; }

    /// <summary>True if "server" ever reached Running — i.e. the gate opened despite the failure.</summary>
    public bool ServerReachedRunning { get; private set; }

    /// <summary>The last state "server" was observed in, for failure-message context.</summary>
    public string? LastServerState => _lastServerState;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Podkop_AppHost>();

        var migrations = builder.Resources.OfType<ProjectResource>().SingleOrDefault(r => r.Name == "migrations")
            ?? throw new InvalidOperationException(
                "The AppHost no longer models a 'migrations' project resource, so the failure path cannot be exercised.");
        builder.CreateResourceBuilder(migrations)
            .WithEnvironment("PODKOP_MIGRATIONS_FAULT", "simulated failing migration");

        _app = await builder.BuildAsync();

        var notifications = _app.ResourceNotifications;
        using var window = new CancellationTokenSource(StartupWindow);
        var watchTask = WatchEventsAsync(notifications, window.Token);

        try
        {
            await _app.StartAsync(window.Token);
        }
        catch (Exception exception)
        {
            StartupFailure = exception;
        }

        if (StartupFailure is null)
        {
            try
            {
                MigrationsTerminal = await _migrationsTerminal.Task.WaitAsync(window.Token);

                try
                {
                    // The bounded negative: a broken gate starts the server right after the
                    // worker's exit; a held gate leaves this wait to time out.
                    ServerReachedRunning = await _serverRunning.Task.WaitAsync(GateGracePeriod, window.Token);
                }
                catch (TimeoutException)
                {
                    ServerReachedRunning = false;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Freeze observations before any test reads them.
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

            if (resourceEvent.Resource.Name == "migrations" && KnownResourceStates.TerminalStates.Contains(state))
            {
                _migrationsTerminal.TrySetResult((state, resourceEvent.Snapshot.ExitCode));
            }

            if (resourceEvent.Resource.Name == "server")
            {
                if (!string.IsNullOrEmpty(state))
                {
                    _lastServerState = state;
                }

                if (state == KnownResourceStates.Running)
                {
                    _serverRunning.TrySetResult(true);
                }
            }
        }
    }
}

/// <summary>
///     The failure half of issue #87's gate contract: when a registered migration fails, the
///     worker must exit non-zero and the API must never start. Exercised through the worker's
///     test-only fault hook; note the pre-fix behavior these tests pin against was empirically a
///     silent exit 0 that opened the gate. Green here is never vacuous: a fault hook that failed
///     to fire would itself exit the worker with code 0 and fail the first test.
/// </summary>
[Collection(OrchestrationTestsCollection.Name)]
public sealed class MigrationFailureSmokeTests(MigrationFailureFixture fixture)
    : IClassFixture<MigrationFailureFixture>
{
    [Fact]
    public void A_failing_migration_exits_the_worker_with_a_nonzero_code()
    {
        Assert.True(
            fixture.StartupFailure is null,
            $"The orchestration failed to start: {fixture.StartupFailure?.Message}");
        Assert.True(
            fixture.MigrationsTerminal is not null,
            "The 'migrations' resource never reached a terminal state within the startup window.");
        Assert.True(
            fixture.MigrationsTerminal!.Value.ExitCode != 0,
            "The worker exited with code 0 despite a failing migration — a success exit code is "
            + "exactly what opens the API's startup gate.");
    }

    [Fact]
    public void A_failing_migration_keeps_the_api_from_starting()
    {
        Assert.True(
            fixture.StartupFailure is null,
            $"The orchestration failed to start: {fixture.StartupFailure?.Message}");
        Assert.True(
            fixture.MigrationsTerminal is not null,
            "The 'migrations' resource never reached a terminal state, so the gate was never "
            + "put to the test.");
        Assert.False(
            fixture.ServerReachedRunning,
            "The API started although the migration worker failed — 'server' must be held back "
            + $"when 'migrations' exits non-zero (last observed server state: {fixture.LastServerState ?? "none"}).");
    }
}
