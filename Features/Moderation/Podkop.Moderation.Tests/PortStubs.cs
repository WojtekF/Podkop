using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Tests;

/// <summary>
///     Test doubles for the ports through which the Moderation slice sees the rest of the world
///     (ADR 0003). Stubbing the ports keeps these tests inside the slice boundary: what the
///     Findings, FindingComments, and Documents slices would answer is pinned here, while the
///     composition-root adapters that produce the real answers are specified in
///     Podkop.Server.Tests.
/// </summary>
internal sealed class StubStatuteLookup(CurrentStatute? current) : IStatuteLookup
{
    public Task<CurrentStatute?> GetCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(current);
}

internal sealed class StubReportTargetLookup(params (ReportTargetKind Kind, ReportTarget Target)[] targets)
    : IReportTargetLookup
{
    public Task<ReportTarget?> GetAsync(ReportTargetKind targetKind, Guid targetId,
        CancellationToken cancellationToken) =>
        Task.FromResult(targets
            .Where(known => known.Kind == targetKind && known.Target.Id == targetId)
            .Select(known => known.Target)
            .FirstOrDefault());
}

internal sealed class StubFindingCommentsLookup(Guid findingId, params Guid[] commentIds) : IFindingCommentsLookup
{
    public Task<IReadOnlyList<Guid>?> GetCommentIdsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>?>(id == findingId ? commentIds : null);
}
