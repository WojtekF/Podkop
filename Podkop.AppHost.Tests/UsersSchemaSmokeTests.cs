using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Npgsql;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.AppHost.Tests;

/// <summary>What the orchestrated database holds once the migration worker has completed.</summary>
public sealed record UsersSchemaSnapshot(
    bool SchemaExists,
    bool HistoryTableExists,
    long AppliedMigrationCount,
    IReadOnlyDictionary<string, string> UserColumnTypes,
    IReadOnlyList<SeededUser> Users)
{
    public static async Task<UsersSchemaSnapshot> ReadAsync(
        string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var schemaExists = await ScalarAsync<bool>(
            connection,
            "select exists (select 1 from information_schema.schemata where schema_name = 'users')",
            cancellationToken);
        var historyTableExists = await ScalarAsync<bool>(
            connection,
            "select exists (select 1 from information_schema.tables "
            + "where table_schema = 'users' and table_name = '__EFMigrationsHistory')",
            cancellationToken);
        var appliedMigrationCount = historyTableExists
            ? await ScalarAsync<long>(
                connection, "select count(*) from users.\"__EFMigrationsHistory\"", cancellationToken)
            : 0L;

        var columnTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(
                         "select column_name, data_type from information_schema.columns "
                         + "where table_schema = 'users' and table_name = 'users'", connection))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columnTypes[reader.GetString(0)] = reader.GetString(1);
            }
        }

        // Rows are read only once the columns they would come from are actually there, so a
        // half-shaped schema reports as a shape problem rather than as a query blowing up.
        var users = new List<SeededUser>();
        if (columnTypes.ContainsKey("user_name") && columnTypes.ContainsKey("role"))
        {
            // Cast to text so a role stored as something else still reads back — the column's
            // declared type is asserted on its own. The C collation orders by bytes, matching
            // the StringComparer.Ordinal the expectations sort with; the database's own
            // collation interleaves cases and would order the same rows differently.
            await using var command = new NpgsqlCommand(
                """select user_name, role::text from users.users order by user_name collate "C" """,
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new SeededUser(reader.GetString(0), reader.GetString(1)));
            }
        }

        return new UsersSchemaSnapshot(
            schemaExists, historyTableExists, appliedMigrationCount, columnTypes, users);
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

public sealed record SeededUser(string UserName, string Role);

/// <summary>
///     Boots the whole orchestration <b>twice</b> against the same persistent PostgreSQL
///     container and data volume, recording what the database holds after each run. The second
///     run is what makes the seed's idempotency observable: the volume survives, so a seed step
///     that re-inserts on every start shows up as a grown or changed population. Two boots are
///     expensive, and this is the only place in issue #88 where a real database exists — the
///     slice's own tests move onto Testcontainers in issue #89, and these seed assertions can
///     move down with them then. A startup failure (no container runtime, say) is recorded rather
///     than thrown, so the tests report the real reason instead of a fixture stack trace.
/// </summary>
public sealed class UsersSchemaFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupWindow = TimeSpan.FromMinutes(5);

    /// <summary>Why a run failed to start (e.g. no container runtime), if one did.</summary>
    public Exception? StartupFailure { get; private set; }

    public int? FirstRunExitCode { get; private set; }

    public int? SecondRunExitCode { get; private set; }

    /// <summary>The database after the first orchestration run; null if that run never completed.</summary>
    public UsersSchemaSnapshot? AfterFirstRun { get; private set; }

    /// <summary>The database after a second run over the surviving data volume.</summary>
    public UsersSchemaSnapshot? AfterSecondRun { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            (FirstRunExitCode, AfterFirstRun) = await RunOnceAsync();
            (SecondRunExitCode, AfterSecondRun) = await RunOnceAsync();
        }
        catch (Exception exception)
        {
            StartupFailure = exception;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<(int? ExitCode, UsersSchemaSnapshot? Snapshot)> RunOnceAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Podkop_AppHost>();
        await using var app = await builder.BuildAsync();

        using var window = new CancellationTokenSource(StartupWindow);
        var terminal = new TaskCompletionSource<(string? State, int? ExitCode)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var watchTask = WatchMigrationsAsync(app.ResourceNotifications, terminal, window.Token);

        try
        {
            await app.StartAsync(window.Token);

            var outcome = await terminal.Task.WaitAsync(window.Token);
            if (outcome.ExitCode != 0)
            {
                return (outcome.ExitCode, null);
            }

            var connectionString = await app.GetConnectionStringAsync("podkopdb", window.Token);
            return (outcome.ExitCode, await UsersSchemaSnapshot.ReadAsync(connectionString!, window.Token));
        }
        catch (OperationCanceledException)
        {
            // The worker never reached a terminal state inside the window; reported as "no run".
            return (null, null);
        }
        finally
        {
            window.Cancel();
            try
            {
                await watchTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task WatchMigrationsAsync(
        ResourceNotificationService notifications,
        TaskCompletionSource<(string? State, int? ExitCode)> terminal,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var resourceEvent in notifications.WatchAsync(cancellationToken))
            {
                var state = resourceEvent.Snapshot.State?.Text;
                if (resourceEvent.Resource.Name == "migrations"
                    && KnownResourceStates.TerminalStates.Contains(state))
                {
                    terminal.TrySetResult((state, resourceEvent.Snapshot.ExitCode));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

/// <summary>
///     The Users slice's schema as PostgreSQL actually holds it after orchestration (issue #88):
///     the slice's own schema carrying its own migrations history, identifiers spelled the way
///     ADR 0010 spells them, the role stored as readable text, the sample users present — and a
///     population a second orchestration run leaves exactly as it found it.
/// </summary>
[Collection(OrchestrationTestsCollection.Name)]
public sealed class UsersSchemaSmokeTests(UsersSchemaFixture fixture) : IClassFixture<UsersSchemaFixture>
{
    [Fact]
    public void The_slice_owns_a_schema_with_its_own_migrations_history()
    {
        AssertTheRunHappened();

        Assert.True(fixture.AfterFirstRun!.SchemaExists, "The database has no 'users' schema.");
        Assert.True(
            fixture.AfterFirstRun.HistoryTableExists,
            "The 'users' schema holds no migrations history table of its own — every converting "
            + "slice would otherwise contend on one database-wide history table (ADR 0010).");
        Assert.True(
            fixture.AfterFirstRun.AppliedMigrationCount > 0,
            "The slice's history table records no applied migration, so nothing created its tables.");
    }

    [Fact]
    public void Identifiers_are_spelled_the_way_the_database_spells_them()
    {
        AssertTheRunHappened();

        Assert.Equal(
            new[] { "role", "user_name" },
            fixture.AfterFirstRun!.UserColumnTypes.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void The_role_is_stored_as_readable_text()
    {
        AssertTheRunHappened();

        Assert.True(
            fixture.AfterFirstRun!.UserColumnTypes.TryGetValue("role", out var roleType),
            "The users table has no 'role' column.");
        Assert.Equal("text", roleType);
    }

    [Fact]
    public void The_sample_users_are_seeded_with_ada_and_grace_as_the_moderators()
    {
        AssertTheRunHappened();

        var expected = SampleUsers.Generate()
            .Select(user => new SeededUser(user.UserName, user.Role.ToString()))
            .OrderBy(user => user.UserName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, fixture.AfterFirstRun!.Users.ToArray());
        Assert.Equal(
            new[] { "ada_lovelace", "grace_hopper" },
            fixture.AfterFirstRun.Users
                .Where(user => user.Role == nameof(UserRole.Moderator))
                .Select(user => user.UserName)
                .ToArray());
    }

    [Fact]
    public void A_second_orchestration_run_leaves_the_seeded_users_exactly_as_it_found_them()
    {
        AssertTheRunHappened();

        Assert.Equal(0, fixture.SecondRunExitCode);
        Assert.NotNull(fixture.AfterSecondRun);

        // The data volume survives between runs, so a seed step that inserts unconditionally
        // shows up here as a duplicated population — or, with a key in the way, as a second run
        // that failed outright and never opened the API's gate.
        Assert.Equal(fixture.AfterFirstRun!.Users.ToArray(), fixture.AfterSecondRun!.Users.ToArray());
        Assert.Equal(fixture.AfterFirstRun.AppliedMigrationCount, fixture.AfterSecondRun.AppliedMigrationCount);
    }

    private void AssertTheRunHappened()
    {
        Assert.True(
            fixture.StartupFailure is null,
            $"The orchestration failed to start: {fixture.StartupFailure?.Message}");
        Assert.True(
            fixture.FirstRunExitCode == 0,
            "The migration worker did not run to successful completion, so nothing can be said "
            + "about the schema it was meant to migrate and seed — is Docker running, and does the "
            + "worker apply the Users slice's migrations? (exit code: "
            + $"{fixture.FirstRunExitCode?.ToString() ?? "never terminated"})");
        Assert.NotNull(fixture.AfterFirstRun);
    }
}
