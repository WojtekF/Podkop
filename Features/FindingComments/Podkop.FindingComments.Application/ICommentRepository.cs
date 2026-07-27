using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetByFindingIdAsync(Guid findingId, CancellationToken cancellationToken);
}
