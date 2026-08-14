using System.Diagnostics;
using Podkop.FindingComments.Application;
using Podkop.Findings.Application;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the Moderation slice's <see cref="IReportTargetLookup"/>
/// port from the repository of whichever slice owns the targeted kind — Findings for findings,
/// FindingComments for comments (issue #33). Slices never reference each other's internals
/// (ADR 0003) — only the host sees both sides, so the bridge lives here.
/// </summary>
internal sealed class ContentBackedReportTargetLookup(IFindingRepository findings, ICommentRepository comments)
    : IReportTargetLookup
{
    public async Task<ReportTarget?> GetAsync(ReportTargetKind targetKind, Guid targetId,
        CancellationToken cancellationToken)
    {
        switch (targetKind)
        {
            case ReportTargetKind.Finding:
                var finding = await findings.GetByIdAsync(targetId, cancellationToken);
                return finding is null ? null : new ReportTarget(finding.Id, finding.Author);
            case ReportTargetKind.Comment:
                var comment = await comments.GetByIdAsync(targetId, cancellationToken);
                return comment is null ? null : new ReportTarget(comment.Id, comment.Author);
            default:
                throw new UnreachableException($"Unmapped report target kind '{targetKind}'.");
        }
    }
}
