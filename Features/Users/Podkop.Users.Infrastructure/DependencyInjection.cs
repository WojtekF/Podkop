using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.Users.Application;
using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsers(
        this IServiceCollection services,
        Func<IReadOnlyList<User>> userSeed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetMyUser>());
        // The seed is a lazy factory: hosts and tests that override the repository never
        // trigger (or pay for) sample-content generation.
        services.AddSingleton<IUserRepository>(_ => new InMemoryUserRepository(userSeed()));
        return services;
    }

    /// <summary>
    ///     Registers <see cref="UsersDbContext" /> against the orchestrated database for any host
    ///     that needs it (issue #88, ADR 0010): it must reach PostgreSQL over the host's
    ///     <c>podkopdb</c> connection, resolve its migrations from this slice's own assembly, and
    ///     record the ones it has applied in a history table living inside the slice's own schema
    ///     — never the database-wide default, which every converting slice would otherwise
    ///     collide on. Registration must also give the context what the orchestration expects of
    ///     a database client: a health check, connection retries, logging and telemetry.
    ///     Only <c>Podkop.MigrationService</c> calls this for now; the API host still answers
    ///     my-user from memory until issue #89 moves it.
    /// </summary>
    public static IHostApplicationBuilder AddUsersPersistence(this IHostApplicationBuilder builder) =>
        throw new NotImplementedException();
}
