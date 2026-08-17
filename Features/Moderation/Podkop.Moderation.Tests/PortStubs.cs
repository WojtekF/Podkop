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
internal sealed class StubStatuteLookup(
    CurrentStatute? current, params (Guid PointId, int Version, CitedPoint Point)[] points) : IStatuteLookup
{
    public Task<CurrentStatute?> GetCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(current);

    public Task<CitedPoint?> GetPointAsync(Guid statutePointId, int version,
        CancellationToken cancellationToken) =>
        Task.FromResult(points
            .Where(known => known.PointId == statutePointId && known.Version == version)
            .Select(known => known.Point)
            .FirstOrDefault());
}

internal sealed class StubModeratorLookup(params string[] moderators) : IModeratorLookup
{
    public Task<bool> IsModeratorAsync(string userName, CancellationToken cancellationToken) =>
        Task.FromResult(moderators.Contains(userName));
}

internal sealed class StubCaseContentLookup(params (ReportTargetKind Kind, Guid Id, CaseContent Content)[] contents)
    : ICaseContentLookup
{
    public Task<CaseContent?> GetAsync(ReportTargetKind targetKind, Guid targetId,
        CancellationToken cancellationToken) =>
        Task.FromResult(contents
            .Where(known => known.Kind == targetKind && known.Id == targetId)
            .Select(known => known.Content)
            .FirstOrDefault());
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
