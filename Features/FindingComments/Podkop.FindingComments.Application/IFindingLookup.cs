namespace Podkop.FindingComments.Application;

/// <summary>
///     The one fact this slice needs from the Findings slice: whether a finding with a given
///     id exists, so the comments endpoint can tell "no comments yet" (an empty discussion)
///     apart from "no such finding" (404). Features never reference each other's internals
///     (ADR 0003), so the composition root implements this port over the Findings slice.
/// </summary>
public interface IFindingLookup
{
    Task<bool> ExistsAsync(Guid findingId, CancellationToken cancellationToken);
}
