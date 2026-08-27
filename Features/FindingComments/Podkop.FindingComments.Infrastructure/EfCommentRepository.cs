using MediatR;
using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     The durable answer to <see cref="ICommentRepository" /> (issue #68): comments live in the
///     slice's PostgreSQL schema, reached through <see cref="FindingCommentsDbContext" />. The
///     finding lookup answers every comment hanging under that finding — top-level comments and
///     replies alike, and nothing from any other finding — each one rehydrated whole: parent
///     reference, author, text, timestamp, and every recorded vote read back exactly as they
///     were written, because both counts and the reader's own highlighted vote are derived from
///     them; thread composition (best-first, chronological replies) stays the query handler's
///     job, exactly as in memory. The id lookup answers the one comment with that id, or null.
///     Adding hands a newly posted comment to the request's context and publishes the slice's
///     contract events translated from the aggregate's domain events (ADR 0003) — through the
///     request's own publisher, the scope lesson issue #96 settled. Loaded and added comments
///     stay tracked: nothing is durable until the use case commits through the slice's
///     <see cref="IUnitOfWork" />. The specs in <c>Podkop.FindingComments.Tests</c> pin all of
///     this against the live database.
/// </summary>
public sealed class EfCommentRepository(FindingCommentsDbContext context, IPublisher publisher)
    : ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> GetByFindingIdAsync(
        Guid findingId, CancellationToken cancellationToken) =>
        await context.Comments
            .AsNoTracking()
            .Where(comment => comment.FindingId == findingId)
            .ToListAsync(cancellationToken);

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken) =>
        context.Comments
            .SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken)
    {
        await publisher.Publish(new CommentPosted(comment.Id, comment.FindingId), cancellationToken);
        await context.AddAsync(comment, cancellationToken);
    }
}
