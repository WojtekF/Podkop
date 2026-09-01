using Podkop.Findings.Domain;
using Podkop.Shared.Domain;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Contracts;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     What this slice lets the rest of the system hear about (ADR 0014, ADR 0003). The slice
///     records several things about a finding; only some of them are anyone else's business, and
///     the ones that are must leave as public, primitive-only contract events — never as the
///     internal domain events themselves, which stay this slice's own vocabulary and must never
///     become part of a durable format other slices read.
///     <para>
///         Two things cross the boundary today, both of them the tag namespace's business (ADR
///         0009/0011): a finding's tag set as it now stands, and a finding having gone away. The
///         announcement of a tag set has to carry the finding's own creation time rather than the
///         moment of the edit, because that is what a tag page orders by. Anything else the slice
///         records — a vote, a promotion, a comment counted — is its own business today: promotion
///         stays an internal one-way fact (ADR 0001), and the index deliberately carries no scores
///         (ADR 0011), so a vote has nothing to announce. An event with nothing to announce yields
///         nothing rather than an empty announcement, so no row is written for it.
///     </para>
///     Specified by <c>FindingsContractEventTranslatorTests</c>.
/// </summary>
public sealed class FindingsContractEventTranslator : IContractEventTranslator
{
    public object? Translate(IDomainEvent domainEvent) => throw new NotImplementedException();
}
