using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

public sealed class InMemoryCommentRepository(IEnumerable<Comment> comments) : ICommentRepository
{
    private readonly IReadOnlyList<Comment> _comments = comments.ToList();

    public Task<IReadOnlyList<Comment>> GetByFindingIdAsync(Guid findingId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Comment>>(
            _comments.Where(comment => comment.FindingId == findingId).ToList());
}
