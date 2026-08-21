using Microsoft.AspNetCore.Mvc.Testing;

namespace Podkop.Shared.Testing;

/// <summary>
///     The connection-string plumbing between a started test database and the host under test
///     (issue #89): production hosts learn where <c>podkopdb</c> lives from the orchestration,
///     but a <see cref="WebApplicationFactory{TEntryPoint}" /> boots the host with nothing
///     orchestrated around it, so the fixture's database has to enter through the same
///     configuration seam the orchestration would have filled. Specs using this override no
///     service: whatever repository the production wiring resolves is what answers — which is
///     exactly what the endpoint specs mean to prove.
/// </summary>
public static class PodkopDatabaseWebApplicationFactoryExtensions
{
    /// <summary>
    ///     A factory whose host reads <paramref name="connectionString" /> as the
    ///     <c>podkopdb</c> connection — early enough that everything registered against that
    ///     name (contexts, their health checks, their telemetry) sees the test database and
    ///     nothing else.
    /// </summary>
    public static WebApplicationFactory<TEntryPoint> WithPodkopDatabase<TEntryPoint>(
        this WebApplicationFactory<TEntryPoint> factory,
        string connectionString)
        where TEntryPoint : class => throw new NotImplementedException();
}
