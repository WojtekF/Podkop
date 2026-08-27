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
        // Singleton state, scoped behavior (issue #96): the store keeps the comments alive for
        // the process, while each request's repository publishes through that request's own
        // IPublisher — a root-bound publisher would resolve CommentPosted consumers where their
        // scoped dependencies cannot follow. The seed stays a lazy factory: hosts and tests that
        // override ICommentRepository never resolve the store, so they never trigger (or pay
        // for) sample-data generation.
        services.AddSingleton(_ => new InMemoryCommentStore(seed()));
        services.AddScoped<ICommentRepository, InMemoryCommentRepository>();
        return services;
    }
}
