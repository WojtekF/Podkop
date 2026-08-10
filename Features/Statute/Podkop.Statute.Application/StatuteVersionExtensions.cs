using Podkop.Statute.Domain;

namespace Podkop.Statute.Application;

public static class StatuteVersionExtensions
{
    public static StatuteDetail ToStatuteDetail(this StatuteVersion? statuteVersion) =>
        statuteVersion is not null
            ? new StatuteDetail(
                statuteVersion.Version,
                statuteVersion.EffectiveFrom,
                statuteVersion.Sections
                    .Select(section => new StatuteSectionDetail(
                        section.Number,
                        section.Title,
                        section.Points
                            .Select(point =>
                                new StatutePointDetail(
                                    point.Id,
                                    point.Number,
                                    point.Text,
                                    point.IsReportable)).ToList()))
                    .ToList())
            : null;
}
