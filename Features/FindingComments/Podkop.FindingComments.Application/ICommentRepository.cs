using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetByFindingIdAsync(Guid findingId, CancellationToken cancellationToken);
    Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>
    ///     Persists a newly posted comment. Persistence also publishes the slice's contract
    ///     events translated from the aggregate's domain events (ADR 0003) — a consumer can
    ///     never observe one without the other.
    /// </summary>
    Task AddAsync(Comment comment, CancellationToken cancellationToken);
}
