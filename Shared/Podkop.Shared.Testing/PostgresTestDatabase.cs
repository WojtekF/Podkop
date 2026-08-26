using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace Podkop.Shared.Testing;

/// <summary>
///     The slice test projects' bridge to real PostgreSQL (issue #89, ADR 0010): behavior specs
///     run against the real engine — never a fake or a lighter database — so Docker is a hard
///     requirement wherever these suites run, dev machines and CI alike. One instance serves a
///     whole test collection, because bringing the engine up is the expensive part; isolation
///     between specs comes from <see cref="ResetAsync" />, never from a fresh database per spec.
///     Before any spec runs, the database must hold the owning slice's schema, brought up by
///     <see cref="MigrateAsync" /> — a derived class per slice supplies that step; everything
///     slice-agnostic lives here.
/// </summary>
public abstract class PostgresTestDatabase : IAsyncLifetime
{
    /// <summary>
    ///     The engine the orchestration runs (<c>Aspire.Hosting.PostgreSQL</c>'s image), pinned
    ///     here because the container tooling's own default trails it by major versions — specs
    ///     passing on an engine the running system never uses prove nothing.
    /// </summary>
    private const string PostgresImage = "postgres:18.3";

    /// <summary>
    ///     The table every slice records its applied migrations in. It lives inside the slice's
    ///     own schema rather than the database-wide default (ADR 0010), so a reset cleaning that
    ///     schema wholesale would take the history with it; ignored by name alone, no slice's
    ///     schema has to be named here.
    /// </summary>
    private static readonly Table MigrationsHistory = new("__EFMigrationsHistory");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgresImage).Build();

    private Respawner? _respawner;

    /// <summary>
    ///     How specs and the application factory reach the started database. Meaningful only
    ///     once <see cref="IAsyncLifetime.InitializeAsync" /> has completed.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    ///     Brings the collection's database up: a real PostgreSQL running the engine version the
    ///     orchestration runs — not whatever aging default the container tooling ships — then the
    ///     owning slice's schema via <see cref="MigrateAsync" />, then whatever the reset
    ///     machinery needs prepared, so spec classes can call <see cref="ResetAsync" /> before
    ///     every spec.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await MigrateAsync(ConnectionString, CancellationToken.None);

        // After the migrations, never before: the reset machinery snapshots the tables it finds,
        // and one built against an empty database would quietly reset nothing.
        await using var connection = await OpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [MigrationsHistory],
        });
    }

    /// <summary>Returns everything the collection borrowed — no container outlives the run.</summary>
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    ///     The isolation seam between specs: afterwards, every row the previous spec put in is
    ///     gone, while everything the migrations created — the schema, its tables, and the
    ///     slice's own migrations-history — survives untouched, so the next spec starts on an
    ///     empty but fully migrated database and <see cref="MigrateAsync" /> never runs twice.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException(
                $"The database has not been brought up yet — {nameof(InitializeAsync)} must run first.");
        }

        await using var connection = await OpenConnectionAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    ///     The owning slice's schema, brought up the way the migration worker brings the
    ///     orchestrated database up: by applying the slice's own checked-in migrations against
    ///     the given connection — never by letting a model synthesize tables, which would bypass
    ///     the migration stream the running system lives on.
    /// </summary>
    protected abstract Task MigrateAsync(string connectionString, CancellationToken cancellationToken);

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
