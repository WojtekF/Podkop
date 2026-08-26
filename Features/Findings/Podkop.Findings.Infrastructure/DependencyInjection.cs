using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.Findings.Application;

namespace Podkop.Findings.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    ///     Registers the slice's use cases and its EF-backed repository (issue #67). The entry
    ///     point takes no seed any more: findings live only in PostgreSQL, and sample findings
    ///     reach the database exclusively through the migration worker — the API host neither
    ///     holds nor triggers a finding seed. Hosts that resolve the repository pair this with
    ///     <see cref="AddFindingsPersistence" />, which registers the context it reads through.
    /// </summary>
    public static IServiceCollection AddFindings(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetMainPageFeed>());
        // Scoped: the repository reads through the slice's context, whose lifetime is the request.
        services.AddScoped<IFindingRepository, EfFindingRepository>();
        return services;
    }

    /// <summary>
    ///     Registers <see cref="FindingsDbContext" /> against the orchestrated database for any
    ///     host that needs it (issue #67, ADR 0010): it reaches PostgreSQL over the host's
    ///     <c>podkopdb</c> connection, resolves its migrations from this slice's own assembly,
    ///     and records the ones it has applied in a history table living inside the slice's own
    ///     schema — never the database-wide default, which every converting slice would otherwise
    ///     collide on. Registration also gives the context what the orchestration expects of a
    ///     database client: a health check, connection retries, logging and telemetry. Both hosts
    ///     call this — the worker to migrate and seed, the API host to answer the feed, the
    ///     detail, and the votes from the same database.
    /// </summary>
    public static IHostApplicationBuilder AddFindingsPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<FindingsDbContext>(options =>
            options.UseFindingsPostgres(builder.Configuration.GetConnectionString("podkopdb")));

        builder.EnrichNpgsqlDbContext<FindingsDbContext>();

        return builder;
    }
}
