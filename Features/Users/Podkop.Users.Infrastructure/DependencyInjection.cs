using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.Users.Application;

namespace Podkop.Users.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    ///     Registers the slice's use cases and its EF-backed repository (issue #89). The entry
    ///     point takes no seed any more: user records live only in PostgreSQL, and sample users
    ///     reach the database exclusively through the migration worker — the API host neither
    ///     holds nor triggers a user seed. Hosts that resolve the repository pair this with
    ///     <see cref="AddUsersPersistence" />, which registers the context it reads through.
    /// </summary>
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetMyUser>());
        // Scoped: the repository reads through the slice's context, whose lifetime is the request.
        services.AddScoped<IUserRepository, EfUserRepository>();
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
    ///     Both hosts call this since issue #89 — the worker to migrate and seed, the API host to
    ///     answer my-user from the same database.
    /// </summary>
    public static IHostApplicationBuilder AddUsersPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<UsersDbContext>(options =>
            options.UseUsersPostgres(builder.Configuration.GetConnectionString("podkopdb")));

        builder.EnrichNpgsqlDbContext<UsersDbContext>();

        return builder;
    }
}
