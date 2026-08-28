using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        // and its contract events publish through the request's own IPublisher, the scope lesson
        // issue #96 settled for this slice's in-memory predecessor.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        // What this slice announces to the rest of the system, in the form the outbox stores
        // (ADR 0014). Stateless, so a singleton. Nothing resolves it yet — the interceptor that
        // will is not on the context until it is implemented; see AddFindingCommentsPersistence.
        services.AddSingleton<IContractEventTranslator, FindingCommentsContractEventTranslator>();
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
    /// </summary>
    public static IHostApplicationBuilder AddFindingCommentsPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<FindingCommentsDbContext>(options =>
            options.UseFindingCommentsPostgres(builder.Configuration.GetConnectionString("podkopdb")));

        // ADR 0014, and deliberately not wired yet. Once OutboxSaveChangesInterceptor is
        // implemented, it goes on the context here — every save through this registration then
        // records the slice's announcements in the same transaction as the state change:
        //
        //     builder.Services.AddDbContext<FindingCommentsDbContext>((serviceProvider, options) =>
        //         options
        //             .UseFindingCommentsPostgres(...)
        //             .AddInterceptors(serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>()));
        //
        // Attaching a throwing interceptor to the running system's context now would take down
        // every save the slice performs, so the seam is specified by OutboxWriteTests — which
        // build a context with the interceptor themselves — and adopted here in the same change
        // that implements it. Until then this slice writes no outbox rows and EfUnitOfWork's
        // publish-after-save remains the only delivery path (issue #94's staged cutover).

        builder.EnrichNpgsqlDbContext<FindingCommentsDbContext>();

        return builder;
    }
}
