using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Domain;
using Podkop.Shared.Infrastructure.Outbox;

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

    /// <summary>
    ///     The slice's own outbox (ADR 0014): the announcements it has made, living in this
    ///     slice's schema like everything else it owns, so one commit covers a discussion
    ///     change and the announcement it causes.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(FindingCommentsDbContextOptions.Schema);
        modelBuilder.AddOutboxMessages();
        modelBuilder.Entity<Comment>(comment =>
        {
            comment.ToTable("comments");
            comment.HasKey(p => p.Id);
            comment.Property(p => p.Id);
            comment.Property(p => p.FindingId);
            comment.Property(p => p.ParentCommentId);
            comment.Property(p => p.Author);
            comment.Property(p => p.Text);
            comment.Property(p => p.CreatedAt);
            comment.OwnsMany(p => p.Votes, vote =>
            {
                vote.ToTable("comment_votes");
                vote.WithOwner().HasForeignKey("comment_id");
                vote.Property(p => p.Voter).HasColumnName("voter");
                vote.HasKey("comment_id", nameof(CommentVoteEntry.Voter));
                vote.Property(p => p.VoteDirection).HasColumnName("vote_direction").HasConversion<string>();
            });
        });
    }
}
