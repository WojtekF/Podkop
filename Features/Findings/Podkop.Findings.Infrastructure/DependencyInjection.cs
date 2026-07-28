using Microsoft.Extensions.DependencyInjection;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFindings(
        this IServiceCollection services,
        Func<IReadOnlyList<Finding>> seed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetMainPageFeed>());
        // The seed is a lazy factory: hosts and tests that override IFindingRepository never
        // trigger (or pay for) sample-data generation. The composition root passes a seed that
        // is coherent with the seeded comment threads (issue #16).
        services.AddSingleton<IFindingRepository>(_ => new InMemoryFindingRepository(seed()));
        return services;
    }
}
