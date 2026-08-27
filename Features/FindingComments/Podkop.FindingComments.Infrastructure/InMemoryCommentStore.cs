using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     The process-lifetime memory behind <see cref="InMemoryCommentRepository" /> (issue #96):
///     comments must outlive any single request, but the repository itself has to stay scoped —
///     the publisher it carries must be the request's own, so <c>CommentPosted</c> consumers
///     resolve in the request scope alongside the scoped services they depend on, never from the
///     root provider. Splitting singleton state from scoped behavior is what makes both true at
///     once. Registered through a lazy factory, so suites that override the repository never
///     trigger sample-data generation.
/// </summary>
public sealed class InMemoryCommentStore(IEnumerable<Comment> comments)
{
    public List<Comment> Comments { get; } = comments.ToList();
}
