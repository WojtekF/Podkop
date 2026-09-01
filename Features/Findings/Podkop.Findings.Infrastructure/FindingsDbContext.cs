using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Domain;
using Podkop.Shared.Infrastructure.Outbox;

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

    /// <summary>
    ///     The slice's own inbox (issue #94, ADR 0014): the announcements it has already acted
    ///     on, living in this slice's schema like everything else it owns, so one commit covers
    ///     an announcement's effect and the memory of having caused it.
    /// </summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(FindingsDbContextOptions.Schema);

        modelBuilder.AddInboxMessages();
        modelBuilder.Entity<Finding>(finding =>
        {
            finding.HasKey(e => e.Id);
            finding.PrimitiveCollection(e => e.Tags);
            finding.Property(f => f.PromotedAt);
            finding.Property(f => f.CommentCount);
            finding.Property(f => f.Title);
            finding.Property(f => f.Description);
            finding.Property(f => f.Source);
            finding.Property(f => f.Thumbnail);
            finding.Property(f => f.Author);
            finding.Property(f => f.CreatedAt);

            finding.OwnsMany<FindingVoteEntry>("_votes", vote =>
            {
                vote.ToTable("finding_votes");
                vote.WithOwner().HasForeignKey("finding_id");
                vote.Property(v => v.Voter).HasColumnName("voter");
                vote.HasKey("finding_id", nameof(FindingVoteEntry.Voter));
                vote.Property(v => v.Side).HasColumnName("side").HasConversion<string>();
                vote.Property(v => v.Reason).HasColumnName("reason").HasConversion<string>();
            });
        });
    }
}
