using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Podkop.FindingComments.Application;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.FindingComments.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    ///     Registers the slice's use cases and its EF-backed repository (issue #68). The entry
    ///     point takes no seed any more: comments live only in PostgreSQL, and sample discussions
    ///     reach the database exclusively through the migration worker — the API host neither
    ///     holds nor triggers a comment seed. Hosts that resolve the repository pair this with
    ///     <see cref="AddFindingCommentsPersistence" />, which registers the context it reads
    ///     through.
    /// </summary>
    public static IServiceCollection AddFindingComments(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetFindingComments>());
        // Scoped: the repository reads and tracks through the slice's context, whose lifetime is
        // the request.
        services.AddScoped<ICommentRepository, EfCommentRepository>();
        // Scoped alongside it (issue #96's pattern): both resolve the same request's context, so
        // the unit of work's one commit makes durable exactly what this request's use case did —
        // announcements included, which the outbox interceptor turns into rows of that same
        // commit (ADR 0014); the commit itself publishes nothing.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }

    /// <summary>
    ///     Registers <see cref="FindingCommentsDbContext" /> against the orchestrated database
    ///     for any host that needs it (issue #68, ADR 0010): it reaches PostgreSQL over the
    ///     host's <c>podkopdb</c> connection, resolves its migrations from this slice's own
    ///     assembly, and records the ones it has applied in a history table living inside the
    ///     slice's own schema — never the database-wide default, which every converting slice
    ///     would otherwise collide on. Registration also gives the context what the orchestration
    ///     expects of a database client: a health check, connection retries, logging and
    ///     telemetry. Both hosts call this — the worker to migrate and seed, the API host to
    ///     answer the discussions and the comment votes from the same database.
    ///     <para>
    ///         The context carries the outbox interceptor (issue #94, ADR 0014): every save this
    ///         slice makes drains what its aggregates raised into <c>outbox_messages</c> rows of
    ///         that same commit, translated to the slice's public contract events. Registration
    ///         is self-contained — the interceptor's translator and clock come along — so any
    ///         host that persists through this slice announces correctly, the worker included
    ///         (its seeds construct aggregates raw and raise nothing, so it announces nothing).
    ///         Reading the outbox back is the API host's business: its
    ///         <c>OutboxProcessingService</c> owns delivery.
    ///     </para>
    /// </summary>
    public static IHostApplicationBuilder AddFindingCommentsPersistence(this IHostApplicationBuilder builder)
    {
        // What this slice announces, and the clock its announcements are stamped with — the
        // interceptor's own dependencies, registered with it so the attachment below can never
        // outrun them in any host. Try-add on purpose: a host that already chose its own
        // TimeProvider keeps it, this is only the fallback for hosts (like the worker) that
        // never cared about time before the interceptor made them.
        builder.Services.AddSingleton<IContractEventTranslator, FindingCommentsContractEventTranslator>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<OutboxSaveChangesInterceptor>();
        builder.Services.AddDbContext<FindingCommentsDbContext>((serviceProvider, options) =>
            options
                .UseFindingCommentsPostgres(builder.Configuration.GetConnectionString("podkopdb"))
                .AddInterceptors(serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>()));

        builder.EnrichNpgsqlDbContext<FindingCommentsDbContext>();

        return builder;
    }
}
