namespace Podkop.Moderation.Application;

/// <summary>
///     What this slice needs from the Documents slice: the Statute version currently in force and
///     which of its points may be cited by a Report. A filed report pins exactly this version
///     (ADR 0006). <c>null</c> when no version is in force yet — nothing can be cited then.
///     Features never reference each other's internals (ADR 0003), so the composition root
///     implements this port over the Documents slice.
/// </summary>
public interface IStatuteLookup
{
    Task<CurrentStatute?> GetCurrentAsync(CancellationToken cancellationToken);
}

/// <summary>The current Statute as this slice sees it: its version and its reportable point ids.</summary>
public sealed record CurrentStatute(int Version, IReadOnlyList<Guid> ReportablePointIds);
