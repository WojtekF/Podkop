using Microsoft.EntityFrameworkCore;

namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     Puts the inbox table into a consuming slice's model (issue #94, ADR 0014). Every consuming
///     slice applies the identical definition to its own schema — the mirror of
///     <see cref="OutboxModelBuilderExtensions" /> on the producing side, and for the same reason:
///     a shared table would be a cross-slice write dependency (ADR 0010). The slice's own
///     <c>HasDefaultSchema</c> and naming convention decide where the table lands and how its
///     identifiers are spelled, so nothing here names a schema.
/// </summary>
public static class InboxModelBuilderExtensions
{
    public static ModelBuilder AddInboxMessages(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(message =>
        {
            message.ToTable("inbox_messages");
            message.HasKey(m => m.Id);
            message.Property(m => m.Id);
            message.Property(m => m.ConsumedAt);
        });

        return modelBuilder;
    }
}
