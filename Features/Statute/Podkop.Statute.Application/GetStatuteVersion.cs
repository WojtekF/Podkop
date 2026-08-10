using MediatR;

namespace Podkop.Statute.Application;

/// <summary>
///     Query for one historical Statute version addressed by its version number — old versions
///     remain readable after an amendment (ADR 0006, issue #30). Yields <c>null</c> when no
///     version carries that number so the endpoint can answer 404.
/// </summary>
public sealed record GetStatuteVersion(int Version) : IRequest<StatuteDetail?>;

public sealed class GetStatuteVersionHandler(IStatuteRepository statuteRepository)
    : IRequestHandler<GetStatuteVersion, StatuteDetail?>
{
    public Task<StatuteDetail?> Handle(GetStatuteVersion request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
