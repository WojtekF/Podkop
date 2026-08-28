namespace Podkop.Shared.Domain;

/// <summary>
///     A fact that happened inside one slice's domain (issue #93). Slices raise their own event
///     records through <see cref="AggregateRoot" /> and keep them internal — infrastructure
///     translates them into public contract events after persistence (ADR 0003). The marker lives
///     in the shared kernel so event infrastructure can handle any slice's events generically;
///     it carries no feature semantics of its own.
/// </summary>
public interface IDomainEvent;
