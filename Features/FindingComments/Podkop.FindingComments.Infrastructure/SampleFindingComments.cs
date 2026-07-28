using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     Development seed data until PostgreSQL persistence lands — the discussion threads for the
///     sample findings. Every sample finding gets at least one thread written by the sample
///     authors: a handful of top-level comments, some of them carrying replies (a reply always
///     points at a top-level comment — never at another reply), with varied up/downvote mixes —
///     including net-score ties — and varied ages, so best-first ordering and chronological
///     replies are visible in the running app. These threads are the authority for each finding's
///     comment count (issue #16): the seed coordination in the composition root aligns
///     Finding.CommentCount with what is generated here, replies included.
/// </summary>
public static class SampleFindingComments
{
    private static readonly string[] Sentences =
    [
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
        "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.",
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium.",
        "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos."
    ];

    private static readonly string[] Authors = ["ada_lovelace", "grace_hopper", "linus_t", "margaret_h", "dennis_r"];

    public static IReadOnlyList<Comment> GenerateFor(IReadOnlyList<Guid> findingIds)
    {
        var topComments = findingIds.SelectMany(guid => GenerateComments(guid)).ToList();
        var replies = topComments.SelectMany(top => GenerateComments(top.FindingId, top.Id));

        return topComments.Concat(replies).ToList();
    }

    private static IEnumerable<Comment> GenerateComments(Guid findingId, Guid? parentCommentId = null)
    {
        return Enumerable.Range(0, Random.Shared.Next(0, 30)).Select(i =>
            new Comment(
                Guid.CreateVersion7(),
                findingId,
                parentCommentId,
                Authors[Random.Shared.Next(Authors.Length)],
                string.Join(" ", Random.Shared.GetItems(Sentences, Random.Shared.Next(1, 4))),
                DateTimeOffset.UtcNow.AddHours(-Random.Shared.Next(2, 96)),
                Random.Shared.Next(0, 100),
                Random.Shared.Next(0, 100)));
    }
}