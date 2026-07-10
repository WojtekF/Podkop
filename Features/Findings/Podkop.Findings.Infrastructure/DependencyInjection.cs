using Microsoft.Extensions.DependencyInjection;
using Podkop.Findings.Application;

namespace Podkop.Findings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFindings(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetMainPageFeed>());
        services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(SampleFindings.Generate()));
        return services;
    }
}
