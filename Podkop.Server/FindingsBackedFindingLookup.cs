using Podkop.FindingComments.Application;
using Podkop.Findings.Application;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the FindingComments slice's <see cref="IFindingLookup"/>
/// port from the Findings slice's repository. Slices never reference each other's internals
/// (ADR 0003) — only the host sees both sides, so the bridge lives here.
/// </summary>
internal sealed class FindingsBackedFindingLookup(IFindingRepository findings) : IFindingLookup
{
    public async Task<bool> ExistsAsync(Guid findingId, CancellationToken cancellationToken) =>
        await findings.GetByIdAsync(findingId, cancellationToken) is not null;
}
