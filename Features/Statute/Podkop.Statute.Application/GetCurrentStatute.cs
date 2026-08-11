using MediatR;

namespace Podkop.Statute.Application;

/// <summary>
///     Query for the Statute version currently in force — the one the public Statute page renders
///     (issue #30). Which version that is, given each version's effective-from date (a published
///     amendment may not be in force yet), is the handler's rule to implement. Yields
///     <c>null</c> when no version is in force so the endpoint can answer 404.
/// </summary>
public sealed record GetCurrentStatute : IRequest<StatuteDetail?>;

public sealed record StatuteDetail(
    int Version,
    DateTimeOffset EffectiveFrom,
    IReadOnlyList<StatuteSectionDetail> Sections);

public sealed record StatuteSectionDetail(
    int Number,
    string Title,
    IReadOnlyList<StatutePointDetail> Points);

public sealed record StatutePointDetail(
    Guid Id,
    int Number,
    string Text,
    bool IsReportable);

public sealed class GetCurrentStatuteHandler(IStatuteRepository statuteRepository, TimeProvider timeProvider)
    : IRequestHandler<GetCurrentStatute, StatuteDetail?>
{
    public async Task<StatuteDetail?> Handle(GetCurrentStatute request, CancellationToken cancellationToken)
    {
        var statutes = await statuteRepository.GetAllVersionsAsync(cancellationToken);

        var statute = statutes
            .Where(s => s.EffectiveFrom <= timeProvider.GetUtcNow())
            .OrderByDescending(s => s.Version)
            .FirstOrDefault();

        return statute.ToStatuteDetail();
    }
}
