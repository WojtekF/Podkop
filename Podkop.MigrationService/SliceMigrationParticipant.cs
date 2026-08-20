using Microsoft.EntityFrameworkCore;

namespace Podkop.MigrationService;

/// <summary>
///     One converted slice's entry in the migration worker's registry (ADR 0010): how to resolve
///     the slice's <see cref="DbContext" /> from a service scope, and the slice's idempotent
///     sample seeder. Each slice registers one of these in <c>Program.cs</c> as it converts to
///     PostgreSQL; the worker walks the participants in registration order. No slice has
///     converted yet (issue #87), so the registered collection resolves empty.
/// </summary>
public sealed record SliceMigrationParticipant(
    string SliceName,
    Func<IServiceProvider, DbContext> ResolveContext,
    Func<IServiceProvider, CancellationToken, Task> SeedAsync);
