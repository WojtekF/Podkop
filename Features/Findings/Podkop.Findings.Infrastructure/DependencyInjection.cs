using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Podkop.Findings.Application;
using Podkop.Shared.Infrastructure.Outbox;

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
        // Scoped alongside it (issue #96): both resolve the same request's context, so the unit
        // of work's one commit makes durable exactly what this request's loads mutated.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        // Scoped for the same reason (issue #94): the inbox tracks consumed announcements
        // through the request's context, so recording one commits with the effect it guards.
        services.AddScoped<IInbox, EfInbox>();
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
    ///     <para>
    ///         Since issue #77 the context carries the outbox interceptor (ADR 0014): every save
    ///         this slice makes drains what its findings raised into <c>outbox_messages</c> rows
    ///         of that same commit, translated to the slice's public contract events — the tag
    ///         announcements the Tags slice indexes (ADR 0009/0011). Registration is
    ///         self-contained — the interceptor's translator and clock come along — so any host
    ///         that persists through this slice announces correctly, the worker included (its
    ///         seeds construct findings raw and raise nothing, so it announces nothing). Reading
    ///         the outbox back is the API host's business: its <c>OutboxProcessingService</c> owns
    ///         delivery.
    ///     </para>
    /// </summary>
    public static IHostApplicationBuilder AddFindingsPersistence(this IHostApplicationBuilder builder)
    {
        // The clock the interceptor stamps rows with. Try-add on purpose: a host that already
        // chose its own TimeProvider keeps it, this is only the fallback for hosts (like the
        // worker) that never cared about time before the interceptor made them.
        builder.Services.TryAddSingleton(TimeProvider.System);
        // Deliberately NOT registered as a shared IContractEventTranslator (as FindingComments
        // does, from the days of being the only producer): that registration holds one
        // translator, so the moment a second slice announces anything, one of the two would
        // silently displace the other and its announcements would vanish. This slice hands its
        // own translator straight to its own interceptor instead, so what a Findings save
        // announces can only ever be decided by the Findings translator.
        builder.Services.AddDbContext<FindingsDbContext>((serviceProvider, options) =>
            options
                .UseFindingsPostgres(builder.Configuration.GetConnectionString("podkopdb"))
                .AddInterceptors(new OutboxSaveChangesInterceptor(
                    new FindingsContractEventTranslator(),
                    serviceProvider.GetRequiredService<TimeProvider>())));

        builder.EnrichNpgsqlDbContext<FindingsDbContext>();

        return builder;
    }
}
