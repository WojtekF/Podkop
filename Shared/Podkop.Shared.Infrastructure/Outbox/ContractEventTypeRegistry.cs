namespace Podkop.Shared.Infrastructure.Outbox;

/// <summary>
///     The contract event types the processor is allowed to resurrect from outbox rows (issue
///     #94, ADR 0014). A row names its event by the type name the write side stored; the
///     processor knows no slice, so the composition root — the one place that sees every slice's
///     Contracts project — hands it this explicit roster at startup. Explicit rather than
///     scanned: an unknown name must fail loudly at the row that carries it, never resolve to
///     whatever a loaded assembly happens to offer.
/// </summary>
public sealed class ContractEventTypeRegistry
{
    private readonly IReadOnlyList<Type> _contractEventTypes;

    public ContractEventTypeRegistry(IEnumerable<Type> contractEventTypes)
    {
        _contractEventTypes = contractEventTypes.ToList();
    }

    /// <summary>
    ///     The registered type an outbox row's stored name identifies. The stored name is the one
    ///     the write side spelled (the type's full name); a name naming no registered type is an
    ///     error, not a <c>null</c> — the row's failure handling is the caller's business, but
    ///     silently resolving nothing must not look like resolving something.
    /// </summary>
    public Type Resolve(string typeName) => throw new NotImplementedException();
}
