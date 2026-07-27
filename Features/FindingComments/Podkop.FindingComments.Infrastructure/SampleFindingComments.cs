using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
/// Development seed data until PostgreSQL persistence lands — the discussion threads for the
/// sample findings. Every sample finding gets at least one thread written by the sample
/// authors: a handful of top-level comments, some of them carrying replies (a reply always
/// points at a top-level comment — never at another reply), with varied up/downvote mixes —
/// including net-score ties — and varied ages, so best-first ordering and chronological
/// replies are visible in the running app. These threads are the authority for each finding's
/// comment count (issue #16): the seed coordination in the composition root aligns
/// Finding.CommentCount with what is generated here, replies included.
/// </summary>
public static class SampleFindingComments
{
    public static IReadOnlyList<Comment> GenerateFor(IReadOnlyList<Guid> findingIds)
    {
        throw new NotImplementedException();
    }
}
