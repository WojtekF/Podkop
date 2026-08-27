using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Application;
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
///     Adding hands a newly posted comment to the request's context and announces nothing:
///     loaded and added comments stay tracked, and the use case's commit through the slice's
///     <see cref="IUnitOfWork" /> is what makes them durable and publishes the contract events
///     translated from what the aggregates raised (ADR 0003). The specs in
///     <c>Podkop.FindingComments.Tests</c> pin all of this against the live database.
/// </summary>
public sealed class EfCommentRepository(FindingCommentsDbContext context) : ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> GetByFindingIdAsync(
        Guid findingId, CancellationToken cancellationToken) =>
        await context.Comments
            .Where(comment => comment.FindingId == findingId)
            .ToListAsync(cancellationToken);

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken) =>
        context.Comments
            .SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken) =>
        await context.AddAsync(comment, cancellationToken);
}
