using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     The FindingComments slice's own context over the shared <c>podkopdb</c> database (issue
///     #68, ADR 0010). Its whole model must land inside the slice's own schema — no table of this
///     context may appear anywhere else — with every table and column spelled the way the ADR
///     spells identifiers, so nothing in psql needs quoting. Comments are keyed by their id. The
///     model has to carry everything a rehydrated comment answers from: the finding it belongs to
///     as a <b>plain uuid column with no cross-schema constraint</b> (the finding lives in another
///     slice's schema — integrity stays at the application level, exactly as in memory), the
///     optional parent comment id that makes it a reply, author, text, when it was written — and
///     every recorded vote, each one the voter's name and the direction taken, because both
///     counts and the reader's own highlighted vote are derived from them. The direction must
///     reach the database as its readable name rather than a number, so values stay legible in
///     psql and survive any future reordering of the enum (ADR 0010). The specs in
///     <c>Podkop.FindingComments.Tests</c> read the shape facts off the built model and prove the
///     round trip against real PostgreSQL.
/// </summary>
public sealed class FindingCommentsDbContext(DbContextOptions<FindingCommentsDbContext> options)
    : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => throw new NotImplementedException();
}
