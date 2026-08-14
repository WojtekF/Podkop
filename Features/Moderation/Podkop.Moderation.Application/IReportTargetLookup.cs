using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     The two facts this slice needs about the content being reported, whichever slice owns it:
///     whether the target exists (<c>null</c> when it does not, so the endpoint can answer 404)
///     and who authored it (self-reports are rejected). Features never reference each other's
///     internals (ADR 0003), so the composition root implements this port over the Findings and
///     FindingComments slices, dispatching on the target kind.
/// </summary>
public interface IReportTargetLookup
{
    Task<ReportTarget?> GetAsync(ReportTargetKind targetKind, Guid targetId, CancellationToken cancellationToken);
}

/// <summary>A piece of reportable content as this slice sees it: its id and its author.</summary>
public sealed record ReportTarget(Guid Id, string Author);
