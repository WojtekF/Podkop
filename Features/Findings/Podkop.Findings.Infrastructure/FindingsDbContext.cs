using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     The Findings slice's own context over the shared <c>podkopdb</c> database (issue #67,
///     ADR 0010). Its whole model must land inside the slice's own schema — no table of this
///     context may appear anywhere else — with every table and column spelled the way the ADR
///     spells identifiers, so nothing in psql needs quoting. Findings are keyed by their id. The
///     model has to carry everything a rehydrated aggregate answers from: title, description,
///     source and optional thumbnail, author, the tags in their given order, both timestamps,
///     the comment count — and every recorded vote, each one the voter's name, the side taken,
///     and (for a bury) its reason, because dig counts and the reader's own highlighted vote are
///     derived from them. The vote side and the bury reason must reach the database as their
///     readable names rather than numbers, so values stay legible in psql and survive any future
///     reordering of the enums (ADR 0010). The specs in <c>Podkop.Findings.Tests</c> read the
///     shape facts off the built model and prove the round trip against real PostgreSQL.
/// </summary>
public sealed class FindingsDbContext(DbContextOptions<FindingsDbContext> options) : DbContext(options)
{
    public DbSet<Finding> Findings => Set<Finding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => throw new NotImplementedException();
}
