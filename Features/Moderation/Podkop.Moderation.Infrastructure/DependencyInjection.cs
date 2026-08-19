using Microsoft.Extensions.DependencyInjection;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddModeration(
        this IServiceCollection services,
        Func<IReadOnlyList<Report>> reportSeed,
        Func<IReadOnlyList<Verdict>> verdictSeed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<FileReport>());
        // Reports were never seeded while they were invisible moderation input (issue #32);
        // the case queue makes them observable (issue #34), so the slice now takes a seed.
        // The seed is a lazy factory: hosts and tests that override the repository never
        // trigger (or pay for) sample-content generation.
        services.AddSingleton<IReportRepository>(_ => new InMemoryReportRepository(reportSeed()));
        // Verdicts seed the same lazy way (issue #35): the Moderation Log and the queue's
        // resolved history are observable as shipped, and overriding hosts and tests never
        // trigger generation.
        services.AddSingleton<IVerdictRepository>(_ => new InMemoryVerdictRepository(verdictSeed()));
        return services;
    }
}
