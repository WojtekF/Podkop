using Podkop.Findings.Domain;

namespace Podkop.Findings.Application;

public static class FindingExtensions
{
    public static FindingSummary MapFindingToFindingSummary(this Finding finding) =>
        new(
            finding.Id,
            finding.Title,
            finding.Description,
            finding.Source.AbsoluteUri,
            finding.Source.Host,
            finding.Thumbnail?.AbsoluteUri,
            finding.Author,
            finding.Tags,
            finding.DigCount,
            finding.CommentCount,
            finding.CreatedAt,
            finding.PromotedAt);
}
