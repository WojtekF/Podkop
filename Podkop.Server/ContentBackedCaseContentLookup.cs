using System.Diagnostics;
using Podkop.FindingComments.Application;
using Podkop.Findings.Application;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the Moderation slice's <see cref="ICaseContentLookup"/>
/// port from the repository of whichever slice owns the targeted kind (issue #34) — a finding's
/// author and title, with its own page as the case's finding page; a comment's author and full
/// text, with the finding it belongs to as its page. Cutting the preview to the queue's cap is
/// the queue's rule, not this bridge's. Slices never reference each other's internals
/// (ADR 0003) — only the host sees both sides, so the bridge lives here.
/// </summary>
internal sealed class ContentBackedCaseContentLookup(IFindingRepository findings, ICommentRepository comments)
    : ICaseContentLookup
{
    public async Task<CaseContent?> GetAsync(ReportTargetKind targetKind, Guid targetId,
        CancellationToken cancellationToken)
    {
        switch (targetKind)
        {
            case ReportTargetKind.Finding:
                var finding = await findings.GetByIdAsync(targetId, cancellationToken);
                return finding is null ? null : new CaseContent(finding.Author, finding.Title, finding.Id);
            case ReportTargetKind.Comment:
                var comment = await comments.GetByIdAsync(targetId, cancellationToken);
                return comment is null ? null : new CaseContent(comment.Author, comment.Text, comment.FindingId);
            default:
                throw new UnreachableException($"Unmapped report target kind '{targetKind}'.");
        }
    }
}
