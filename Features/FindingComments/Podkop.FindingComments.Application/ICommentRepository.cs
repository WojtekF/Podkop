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
    ///     Hands a newly posted comment to the store. Adding announces nothing: the contract
    ///     events translated from what the aggregate raised (ADR 0003) are published by the
    ///     use case's commit through <see cref="IUnitOfWork" />, after durability — a consumer
    ///     can never see a contract event for a comment that was not stored.
    /// </summary>
    Task AddAsync(Comment comment, CancellationToken cancellationToken);
}
