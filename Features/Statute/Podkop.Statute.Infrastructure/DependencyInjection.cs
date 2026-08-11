using Microsoft.Extensions.DependencyInjection;
using Podkop.Statute.Application;
using Podkop.Statute.Domain;

namespace Podkop.Statute.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStatute(
        this IServiceCollection services,
        Func<IReadOnlyList<StatuteVersion>> statuteSeed,
        Func<IReadOnlyList<PrivacyPolicyVersion>> privacyPolicySeed)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetCurrentStatute>());
        // The seeds are lazy factories: hosts and tests that override the repositories never
        // trigger (or pay for) sample-content generation.
        services.AddSingleton<IStatuteRepository>(_ => new InMemoryStatuteRepository(statuteSeed()));
        services.AddSingleton<IPrivacyPolicyRepository>(_ => new InMemoryPrivacyPolicyRepository(privacyPolicySeed()));
        return services;
    }
}
