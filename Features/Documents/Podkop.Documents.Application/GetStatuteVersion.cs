using MediatR;

namespace Podkop.Documents.Application;

/// <summary>
///     Query for one historical Statute version addressed by its version number — old versions
///     remain readable after an amendment (ADR 0006, issue #30). Yields <c>null</c> when no
///     version carries that number, and also when the version exists but is not yet in force:
///     a published-but-future amendment stays hidden until its effective-from instant, the same
///     gate the current-document query applies. The endpoint answers 404 for both.
/// </summary>
public sealed record GetStatuteVersion(int Version) : IRequest<StatuteDetail?>;

public sealed class GetStatuteVersionHandler(IStatuteRepository statuteRepository, TimeProvider timeProvider)
    : IRequestHandler<GetStatuteVersion, StatuteDetail?>
{
    public async Task<StatuteDetail?> Handle(GetStatuteVersion request, CancellationToken cancellationToken)
    {
        var statutes = await statuteRepository.GetAllVersionsAsync(cancellationToken);

        var statute = statutes.Where(s => s.EffectiveFrom <= timeProvider.GetUtcNow())
            .FirstOrDefault(s => s.Version == request.Version);

        return statute.ToStatuteDetail();
    }
}
