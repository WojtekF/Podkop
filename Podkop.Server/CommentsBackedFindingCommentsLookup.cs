using Podkop.FindingComments.Application;
using Podkop.Findings.Application;
using Podkop.Moderation.Application;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the Moderation slice's <see cref="IFindingCommentsLookup"/>
/// port (issue #33) — null for an unknown finding (the Findings slice's repository decides
/// existence), otherwise the ids of every comment and reply in the finding's discussion from the
/// FindingComments slice's repository. Slices never reference each other's internals (ADR 0003)
/// — only the host sees both sides, so the bridge lives here.
/// </summary>
internal sealed class CommentsBackedFindingCommentsLookup(IFindingRepository findings, ICommentRepository comments)
    : IFindingCommentsLookup
{
    public async Task<IReadOnlyList<Guid>?> GetCommentIdsAsync(Guid findingId, CancellationToken cancellationToken)
    {
        var finding = await findings.GetByIdAsync(findingId, cancellationToken);
        if (finding is null) return null;

        var discussion = await comments.GetByFindingIdAsync(findingId, cancellationToken);
        return discussion.Select(comment => comment.Id).ToList();
    }
}
