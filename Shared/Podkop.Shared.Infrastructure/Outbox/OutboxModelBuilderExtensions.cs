using Microsoft.EntityFrameworkCore;

namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     Puts the outbox table into a slice's model (ADR 0014). Every converted slice applies the
///     identical definition to its own schema rather than sharing one database-wide table, which
///     would be a cross-slice write dependency and a migration every slice contends on (ADR
///     0010). The slice's own <c>HasDefaultSchema</c> and naming convention decide where the
///     table lands and how its identifiers are spelled, so nothing here names a schema.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder AddOutboxMessages(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable("outbox_messages");
            message.HasKey(m => m.Id);
            message.Property(m => m.Id);
            message.Property(m => m.Type);
            message.Property(m => m.Payload);
            message.Property(m => m.OccurredAt);
            message.Property(m => m.ProcessedAt);
            message.Property(m => m.Attempts);
            message.Property(m => m.Error);
        });

        return modelBuilder;
    }
}
