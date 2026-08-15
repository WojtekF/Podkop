using MediatR;
using Podkop.Documents.Application;
using Podkop.Moderation.Application;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the Moderation slice's <see cref="IStatuteLookup"/> port by
/// dispatching the Documents slice's own current-statute query, so the "which version is in
/// force" rule lives in exactly one place (ADR 0006). Slices never reference each other's
/// internals (ADR 0003) — only the host sees both sides, so the bridge lives here. Registered
/// scoped, matching the lifetime of the <see cref="ISender"/> it dispatches through.
/// </summary>
internal sealed class DocumentsBackedStatuteLookup(ISender sender) : IStatuteLookup
{
    public async Task<CurrentStatute?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var statute = await sender.Send(new GetCurrentStatute(), cancellationToken);
        return statute is null
            ? null
            : new CurrentStatute(
                statute.Version,
                statute.Sections
                    .SelectMany(section => section.Points)
                    .Where(point => point.IsReportable)
                    .Select(point => point.Id)
                    .ToList());
    }

    // Resolved through the Documents slice's own version query, so its effective-from gate
    // applies unchanged; a report can only ever pin a version that was in force, so the gate
    // never hides a pinned version.
    public async Task<CitedPoint?> GetPointAsync(Guid statutePointId, int version,
        CancellationToken cancellationToken)
    {
        var statute = await sender.Send(new GetStatuteVersion(version), cancellationToken);
        return statute?.Sections
            .SelectMany(section => section.Points.Select(point => (Section: section, Point: point)))
            .Where(located => located.Point.Id == statutePointId)
            .Select(located => new CitedPoint(located.Section.Number, located.Point.Number, located.Point.Text))
            .FirstOrDefault();
    }
}
