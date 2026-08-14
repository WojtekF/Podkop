namespace Podkop.Moderation.Application;

/// <summary>
///     The one fact the batch my-reports query needs from the FindingComments slice: which
///     comments (top-level and replies alike) belong to a finding's discussion — <c>null</c> when
///     no finding has that id, so the endpoint can answer 404. Features never reference each
///     other's internals (ADR 0003); the composition root implements this port over the Findings
///     and FindingComments slices.
/// </summary>
public interface IFindingCommentsLookup
{
    Task<IReadOnlyList<Guid>?> GetCommentIdsAsync(Guid findingId, CancellationToken cancellationToken);
}
