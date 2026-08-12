using Microsoft.Extensions.DependencyInjection;
using Podkop.Moderation.Application;

namespace Podkop.Moderation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddModeration(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<FileReport>());
        // Reports are member-created moderation input, invisible to members (issue #32) —
        // nothing observable needs seeding, so the repository simply starts empty.
        services.AddSingleton<IReportRepository>(new InMemoryReportRepository([]));
        return services;
    }
}
