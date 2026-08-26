using Podkop.FindingComments.Domain;
using Podkop.Shared.Infrastructure;

namespace Podkop.FindingComments.Infrastructure;

/// <summary>
///     Development seed data — the discussion threads for the sample findings: top-level comments,
///     some of them carrying replies (a reply always points at a top-level comment — never at
///     another reply), with varied up/downvote mixes and varied ages, so best-first ordering and
///     chronological replies are visible in the running app. These threads are the authority for
///     each finding's comment count (issue #16), and since issue #67 the findings live in
///     PostgreSQL while the discussions are still seeded here, in the API host's memory — so each
///     discussion's shape is a deterministic function of its finding's id: the size is the shared
///     <see cref="SampleDiscussions" /> plan's answer — the same number the findings generator
///     stamped on the finding it persisted — and every other choice comes from the id-seeded
///     stream. Only the comment ids stay fresh per process; nothing outside this process ever
///     references them. The stub user (ada_lovelace) must also arrive with scattered pre-existing
///     votes over the generated set — never on her own comments — so vote highlighting is visible
///     on first load (issue #18); like the findings seed, her votes are placed by position rather
///     than by chance.
/// </summary>
public static class SampleFindingComments
{
    /// <summary>
    ///     The stub current user, mirroring <c>StubCurrentUser</c> in the composition root — the
    ///     identity whose own vote the app highlights, so the seed has to place it on purpose.
    /// </summary>
    private const string StubUser = "ada_lovelace";

    public static IReadOnlyList<Comment> GenerateFor(IReadOnlyList<Guid> findingIds) =>
        [.. findingIds.SelectMany(GenerateDiscussion)];

    private static IEnumerable<Comment> GenerateDiscussion(Guid findingId)
    {
        var random = SampleDiscussions.RandomFor(findingId);
        var count = SampleDiscussions.CommentCountFor(findingId);
        var authorsWithoutStub = SampleData.Authors.Remove(StubUser);

        var comments = new List<Comment>(count);
        var topComments = new List<Comment>();
        for (var position = 0; position < count; position++)
        {
            var stubVote = StubVoteFor(position, random);
            // Nobody may vote on their own comment, so the stub user does not get to author the
            // ones her vote is due on — mirroring the findings seed, where a dropped vote would
            // degrade the guaranteed mix to a merely likely one.
            var author = stubVote is null
                ? SampleData.Authors[random.Next(SampleData.Authors.Length)]
                : authorsWithoutStub[random.Next(authorsWithoutStub.Length)];

            var votes = CrowdVotes(random, author);
            if (stubVote is not null) votes[StubUser] = stubVote.Value;

            // Every third position replies to an earlier top-level comment. Position 0 is always
            // top-level, so a parent exists by the time one is needed.
            var parent = position % 3 == 2 ? topComments[random.Next(topComments.Count)] : null;

            var comment = new Comment(
                Guid.CreateVersion7(),
                findingId,
                parent?.Id,
                author,
                string.Join(" ", random.GetItems(SampleData.Lines.AsSpan(), random.Next(1, 4))),
                DateTimeOffset.UtcNow.AddHours(-random.Next(2, 96)),
                votes);
            if (parent is null) topComments.Add(comment);
            comments.Add(comment);
        }

        return comments;
    }

    /// <summary>
    ///     The stub user's vote at this position, or <c>null</c> where she has not voted. Fixed by
    ///     position rather than drawn at random, like her finding votes: every fourth comment from
    ///     the second onward carries one, which lands votes on most multi-comment discussions
    ///     while the rest stays unvoted, so the highlight reads as scattered rather than blanket.
    /// </summary>
    private static VoteDirection? StubVoteFor(int position, Random random) =>
        position % 4 == 1 ? (VoteDirection)random.Next(0, 2) : null;

    private static Dictionary<string, VoteDirection> CrowdVotes(Random random, string author)
    {
        // The crowd excludes the stub user: her side is chosen deliberately above, never
        // inherited from wherever she happens to land in the shuffle.
        var crowd = SampleData.Authors.Where(a => a != author && a != StubUser).ToArray();
        return crowd.OrderBy(_ => random.Next())
            .Take(random.Next(1, crowd.Length))
            .ToDictionary(voter => voter, _ => (VoteDirection)random.Next(0, 2));
    }
}
