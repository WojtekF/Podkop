namespace Podkop.Moderation.Application;

/// <summary>
///     The two facts this slice needs from the Findings slice about the content being reported:
///     whether the finding exists (<c>null</c> when it does not, so the endpoint can answer 404)
///     and who authored it (self-reports are rejected). Features never reference each other's
///     internals (ADR 0003), so the composition root implements this port over the Findings
///     slice.
/// </summary>
public interface IReportTargetLookup
{
    Task<ReportTarget?> GetAsync(Guid findingId, CancellationToken cancellationToken);
}

/// <summary>A piece of reportable content as this slice sees it: its id and its author.</summary>
public sealed record ReportTarget(Guid Id, string Author);
