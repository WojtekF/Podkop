using Podkop.Findings.Application;
using Podkop.Moderation.Application;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the Moderation slice's <see cref="IReportTargetLookup"/>
/// port from the Findings slice's repository. Slices never reference each other's internals
/// (ADR 0003) — only the host sees both sides, so the bridge lives here.
/// </summary>
internal sealed class FindingsBackedReportTargetLookup(IFindingRepository findings) : IReportTargetLookup
{
    public async Task<ReportTarget?> GetAsync(Guid findingId, CancellationToken cancellationToken)
    {
        var finding = await findings.GetByIdAsync(findingId, cancellationToken);
        return finding is null ? null : new ReportTarget(finding.Id, finding.Author);
    }
}
