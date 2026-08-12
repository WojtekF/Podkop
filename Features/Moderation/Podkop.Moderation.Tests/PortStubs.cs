using Podkop.Moderation.Application;

namespace Podkop.Moderation.Tests;

/// <summary>
///     Test doubles for the two ports through which the Moderation slice sees the rest of the
///     world (ADR 0003). Stubbing the ports keeps these tests inside the slice boundary: what
///     the Findings and Documents slices would answer is pinned here, while the composition-root
///     adapters that produce the real answers are specified in Podkop.Server.Tests.
/// </summary>
internal sealed class StubStatuteLookup(CurrentStatute? current) : IStatuteLookup
{
    public Task<CurrentStatute?> GetCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(current);
}

internal sealed class StubReportTargetLookup(params ReportTarget[] targets) : IReportTargetLookup
{
    public Task<ReportTarget?> GetAsync(Guid findingId, CancellationToken cancellationToken) =>
        Task.FromResult(targets.FirstOrDefault(target => target.Id == findingId));
}
