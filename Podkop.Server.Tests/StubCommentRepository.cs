using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;

namespace Podkop.Server.Tests;

/// <summary>
///     The comments store, doubled at the slice's own port: comments live in PostgreSQL since
///     issue #68, so suites whose subject is the host's cross-slice wiring answer discussion
///     facts from a fixed list instead of hauling a database into specs that are not about
///     persistence. Adding lands in the held list; it publishes nothing, because the suites this
///     double serves never post — they read comments through the moderation adapters. Mutations
///     on held comments are visible in place, which is why the paired
///     <see cref="StubCommentsUnitOfWork" /> has nothing left to commit.
/// </summary>
internal sealed class StubCommentRepository(IEnumerable<Comment> comments) : ICommentRepository
{
    private readonly List<Comment> _comments = comments.ToList();

    public Task<IReadOnlyList<Comment>> GetByFindingIdAsync(
        Guid findingId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            _comments.Where(comment => comment.FindingId == findingId).ToList());

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken) =>
        Task.FromResult(_comments.FirstOrDefault(comment => comment.Id == commentId));

    public Task AddAsync(Comment comment, CancellationToken cancellationToken)
    {
        _comments.Add(comment);
        return Task.CompletedTask;
    }
}
