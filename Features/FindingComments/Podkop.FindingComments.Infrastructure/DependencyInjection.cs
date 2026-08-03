using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFindingComments(
        this IServiceCollection services,
        Func<IReadOnlyList<Comment>> seed)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetFindingComments>());
        // The seed is a lazy factory: hosts and tests that override ICommentRepository never
        // trigger (or pay for) sample-data generation.
        services.AddSingleton<ICommentRepository>(provider =>
            new InMemoryCommentRepository(seed(), provider.GetRequiredService<IPublisher>()));
        return services;
    }
}
