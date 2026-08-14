using Microsoft.Extensions.DependencyInjection;
using Podkop.Users.Application;
using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsers(
        this IServiceCollection services,
        Func<IReadOnlyList<User>> userSeed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetMyUser>());
        // The seed is a lazy factory: hosts and tests that override the repository never
        // trigger (or pay for) sample-content generation.
        services.AddSingleton<IUserRepository>(_ => new InMemoryUserRepository(userSeed()));
        return services;
    }
}
