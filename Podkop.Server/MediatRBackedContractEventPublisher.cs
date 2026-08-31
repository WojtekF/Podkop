using MediatR;
using Podkop.Shared.Infrastructure.Outbox;

namespace Podkop.Server;

/// <summary>
///     What publishing means today (issue #94, ADR 0014): the outbox processor hands over a
///     resolved contract event and this adapter pushes it through MediatR to the in-process
///     handlers the composition root has registered — the Findings slice's comment counting,
///     and whatever consumers later slices add. The shared outbox machinery knows no bus on
///     purpose; swapping MediatR for a real broker later means replacing this adapter, nothing
///     else.
/// </summary>
public sealed class MediatRBackedContractEventPublisher(IPublisher publisher) : IContractEventPublisher
{
    public Task PublishAsync(object contractEvent, CancellationToken cancellationToken) =>
        publisher.Publish(contractEvent, cancellationToken);
}
