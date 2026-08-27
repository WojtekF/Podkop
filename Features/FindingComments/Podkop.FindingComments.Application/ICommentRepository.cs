using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

/// <summary>
///     The FindingComments slice's durable store seam, read/track-only since issue #68: loading
///     hands back change-tracked comments rehydrated whole — votes included, because counts and
///     the reader's own highlighted vote are derived from them — and adding tracks a new comment;
///     the slice's <see cref="IUnitOfWork" /> makes whatever a use case did durable in one
///     explicit commit, the repository itself persists nothing.
/// </summary>
public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetByFindingIdAsync(Guid findingId, CancellationToken cancellationToken);
    Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>
    ///     Hands a newly posted comment to the store and publishes the slice's contract events
    ///     translated from the aggregate's domain events (ADR 0003) — the add and the events
    ///     travel together; durability is the use case's commit.
    /// </summary>
    Task AddAsync(Comment comment, CancellationToken cancellationToken);
}
