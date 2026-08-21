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
    ///     How specs and the application factory reach the started database. Meaningful only
    ///     once <see cref="IAsyncLifetime.InitializeAsync" /> has completed.
    /// </summary>
    public string ConnectionString => throw new NotImplementedException();

    /// <summary>
    ///     Brings the collection's database up: a real PostgreSQL running the engine version the
    ///     orchestration runs — not whatever aging default the container tooling ships — then the
    ///     owning slice's schema via <see cref="MigrateAsync" />, then whatever the reset
    ///     machinery needs prepared, so spec classes can call <see cref="ResetAsync" /> before
    ///     every spec.
    /// </summary>
    public Task InitializeAsync() => throw new NotImplementedException();

    /// <summary>Returns everything the collection borrowed — no container outlives the run.</summary>
    public Task DisposeAsync() => throw new NotImplementedException();

    /// <summary>
    ///     The isolation seam between specs: afterwards, every row the previous spec put in is
    ///     gone, while everything the migrations created — the schema, its tables, and the
    ///     slice's own migrations-history — survives untouched, so the next spec starts on an
    ///     empty but fully migrated database and <see cref="MigrateAsync" /> never runs twice.
    /// </summary>
    public Task ResetAsync() => throw new NotImplementedException();

    /// <summary>
    ///     The owning slice's schema, brought up the way the migration worker brings the
    ///     orchestrated database up: by applying the slice's own checked-in migrations against
    ///     the given connection — never by letting a model synthesize tables, which would bypass
    ///     the migration stream the running system lives on.
    /// </summary>
    protected abstract Task MigrateAsync(string connectionString, CancellationToken cancellationToken);
}
