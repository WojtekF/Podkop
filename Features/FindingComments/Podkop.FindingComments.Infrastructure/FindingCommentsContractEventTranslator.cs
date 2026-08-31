using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;
using Podkop.Shared.Domain;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     What this slice lets the rest of the system hear about (ADR 0014, ADR 0003). The slice
///     records several things about a discussion; only some of them are anyone else's business,
///     and the ones that are must leave as the slice's public, primitive-only contract events —
///     never as the internal domain events themselves, which stay this slice's own vocabulary and
///     must never become part of a durable format other slices read.
///     <para>
///         Today exactly one thing crosses the boundary: a comment having been posted, which the
///         Findings slice counts on its finding. Anything else the slice records — a vote, for
///         instance — is its own business and is announced to nobody. An event with nothing to
///         announce yields nothing rather than an empty announcement, so no row is written for it.
///     </para>
///     Specified by <c>FindingCommentsContractEventTranslatorTests</c>.
/// </summary>
public sealed class FindingCommentsContractEventTranslator : IContractEventTranslator
{
    public object? Translate(IDomainEvent domainEvent) =>
        domainEvent switch
        {
            // Guid.Empty is not an identity — every announcement must leave here carrying a
            // fresh one of its own (issue #94); the translator specs define what that means.
            CommentAdded added => new CommentPosted(Guid.Empty, added.CommentId, added.FindingId),
            _ => null
        };
}
