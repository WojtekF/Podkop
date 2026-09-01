using Microsoft.EntityFrameworkCore;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The Tags slice's own context over the shared <c>podkopdb</c> database (issue #77, ADR
///     0010). Its whole model must land inside the slice's own schema — no table of this context
///     may appear anywhere else — with every table and column spelled the way the ADR spells
///     identifiers, so nothing in psql needs quoting.
///     <para>
///         The model has to carry the membership index as ADR 0011 describes it: the canonical
///         tag, the content's type and id, and the content's created-at. Nothing identifies a
///         membership except the three facts that make it one — a piece of content carries a tag
///         once, and hearing the same announcement again must not be able to file it twice — so
///         the row's identity is those facts themselves rather than a surrogate. The content
///         reference is a <b>plain (type, uuid) pair with no cross-schema constraint</b>: the
///         findings and entries it names live in other slices' schemas, and integrity stays at
///         the contract-event level. The content type must reach the database as its readable
///         name rather than a number, so values stay legible in psql and survive any future
///         reordering of the enum (ADR 0010). Answering a tag page means finding one tag's rows
///         in created-at order and skipping deep into them, so the model must let the database do
///         that without reading the tag's whole history first.
///     </para>
///     <para>
///         The slice's own inbox rides along (ADR 0014): the announcements it has already acted
///         on, in this slice's schema like everything else it owns, so one commit covers an index
///         change and the memory of having made it.
///     </para>
///     The specs in <c>Podkop.Tags.Tests</c> read the shape facts off the built model and prove
///     the round trip against real PostgreSQL.
/// </summary>
public sealed class TagsDbContext(DbContextOptions<TagsDbContext> options) : DbContext(options)
{
    public DbSet<TagMembership> TagMemberships => Set<TagMembership>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => throw new NotImplementedException();
}
