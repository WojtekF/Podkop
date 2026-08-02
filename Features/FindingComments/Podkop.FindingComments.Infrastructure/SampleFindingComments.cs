using Podkop.FindingComments.Domain;
using Podkop.Shared.Infrastructure;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     Development seed data until PostgreSQL persistence lands — the discussion threads for the
///     sample findings. Every sample finding can have a thread written by the sample
///     authors: a handful of top-level comments, some of them carrying replies (a reply always
///     points at a top-level comment — never at another reply), with varied up/downvote mixes —
///     including net-score ties — and varied ages, so best-first ordering and chronological
///     replies are visible in the running app. These threads are the authority for each finding's
///     comment count (issue #16): the seed coordination in the composition root aligns
///     Finding.CommentCount with what is generated here, replies included. The stub user
///     (ada_lovelace) must also arrive with scattered pre-existing votes over the generated set —
///     never on her own comments — so vote highlighting is visible on first load (issue #18).
/// </summary>
public static class SampleFindingComments
{
    public static IReadOnlyList<Comment> GenerateFor(IReadOnlyList<Guid> findingIds)
    {
        var topComments = findingIds.SelectMany(guid => GenerateComments(guid)).ToList();
        var replies = topComments.SelectMany(top => GenerateComments(top.FindingId, top.Id));

        return topComments.Concat(replies).ToList();
    }

    private static IEnumerable<Comment> GenerateComments(Guid findingId, Guid? parentCommentId = null)
    {
        return Enumerable.Range(0, Random.Shared.Next(0, 30)).Select(i =>
        {
            var author = SampleData.Authors[Random.Shared.Next(SampleData.Authors.Length)];
            return new Comment(
                Guid.CreateVersion7(),
                findingId,
                parentCommentId,
                author,
                string.Join(" ", Random.Shared.GetItems(SampleData.Lines.AsSpan(), Random.Shared.Next(1, 4))),
                DateTimeOffset.UtcNow.AddHours(-Random.Shared.Next(2, 96)),
                SampleData.Authors.Where(a => a != author)
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(Random.Shared.Next(1, SampleData.Authors.Length - 1))
                    .ToDictionary(key => key, key => (VoteDirection)Random.Shared.Next(0, 2))
            );
        });
    }
}