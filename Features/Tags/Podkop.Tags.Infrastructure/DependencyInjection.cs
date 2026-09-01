using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Podkop.Tags.Application;

namespace Podkop.Tags.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    ///     Registers the slice's use cases and its EF-backed index (issue #77). The entry point
    ///     takes no seed: the index lives only in PostgreSQL, and the sample rows reach the
    ///     database exclusively through the migration worker — the API host neither holds nor
    ///     triggers a tags seed. Hosts that resolve the repository pair this with
    ///     <see cref="AddTagsPersistence" />, which registers the context it reads through.
    /// </summary>
    public static IServiceCollection AddTags(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetTagPage>());
        // Scoped: the repository reads and tracks through the slice's context, whose lifetime is
        // the request — and, for a delivered announcement, the processor's pass.
        services.AddScoped<ITagMembershipRepository, EfTagMembershipRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IInbox, EfInbox>();
        return services;
    }

    /// <summary>
    ///     Registers <see cref="TagsDbContext" /> against the orchestrated database for any host
    ///     that needs it (issue #77, ADR 0010): it reaches PostgreSQL over the host's
    ///     <c>podkopdb</c> connection, resolves its migrations from this slice's own assembly, and
    ///     records the ones it has applied in a history table living inside the slice's own schema
    ///     — never the database-wide default, which every converting slice would otherwise collide
    ///     on. Registration also gives the context what the orchestration expects of a database
    ///     client: a health check, connection retries, logging and telemetry. Both hosts call this
    ///     — the worker to migrate and seed, the API host to answer tag pages and to index the
    ///     announcements delivered to it.
    ///     <para>
    ///         No outbox interceptor here, unlike the content slices': Tags announces nothing. It
    ///         is a pure consumer of the tag namespace (ADR 0009) — everything it writes is the
    ///         effect of somebody else's announcement, and nobody downstream is waiting to hear
    ///         about it.
    ///     </para>
    /// </summary>
    public static IHostApplicationBuilder AddTagsPersistence(this IHostApplicationBuilder builder)
    {
        // The clock the inbox stamps consumed announcements with. Try-add on purpose: a host that
        // already chose its own TimeProvider keeps it; this is only the fallback for hosts (like
        // the worker) that never cared about time before.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddDbContext<TagsDbContext>(options =>
            options.UseTagsPostgres(builder.Configuration.GetConnectionString("podkopdb")));

        builder.EnrichNpgsqlDbContext<TagsDbContext>();

        return builder;
    }
}
