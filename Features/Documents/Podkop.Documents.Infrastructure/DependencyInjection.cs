using Microsoft.Extensions.DependencyInjection;
using Podkop.Documents.Application;
using Podkop.Documents.Domain;

namespace Podkop.Documents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuments(
        this IServiceCollection services,
        Func<IReadOnlyList<StatuteVersion>> statuteSeed,
        Func<IReadOnlyList<PrivacyPolicyVersion>> privacyPolicySeed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetCurrentStatute>());
        // The seeds are lazy factories: hosts and tests that override the repositories never
        // trigger (or pay for) sample-content generation.
        services.AddSingleton<IStatuteRepository>(_ => new InMemoryStatuteRepository(statuteSeed()));
        services.AddSingleton<IPrivacyPolicyRepository>(_ => new InMemoryPrivacyPolicyRepository(privacyPolicySeed()));
        return services;
    }
}
