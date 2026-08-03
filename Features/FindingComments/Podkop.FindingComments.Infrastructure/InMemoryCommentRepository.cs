using MediatR;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

public sealed class InMemoryCommentRepository(IEnumerable<Comment> comments, IPublisher publisher)
    : ICommentRepository
{
    private readonly List<Comment> _comments = comments.ToList();

    public Task<IReadOnlyList<Comment>> GetByFindingIdAsync(Guid findingId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            _comments.Where(comment => comment.FindingId == findingId).ToList());

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken) =>
        Task.FromResult(_comments.FirstOrDefault(comment => comment.Id == commentId));

    /// <summary>
    ///     Persists the comment, then translates its domain events into public contract events
    ///     (ADR 0003): translation happens at the persistence seam so a consumer can never see
    ///     a contract event for a comment that was not stored.
    /// </summary>
    public async Task AddAsync(Comment comment, CancellationToken cancellationToken)
    {
        _comments.Add(comment);
        foreach (var added in comment.DomainEvents.OfType<CommentAdded>())
        {
            await publisher.Publish(new CommentPosted(added.CommentId, added.FindingId), cancellationToken);
        }
    }
}
