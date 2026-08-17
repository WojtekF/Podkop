using Microsoft.Extensions.DependencyInjection;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddModeration(
        this IServiceCollection services,
        Func<IReadOnlyList<Report>> reportSeed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<FileReport>());
        // Reports were never seeded while they were invisible moderation input (issue #32);
        // the case queue makes them observable (issue #34), so the slice now takes a seed.
        // The seed is a lazy factory: hosts and tests that override the repository never
        // trigger (or pay for) sample-content generation.
        services.AddSingleton<IReportRepository>(_ => new InMemoryReportRepository(reportSeed()));
        return services;
    }
}
