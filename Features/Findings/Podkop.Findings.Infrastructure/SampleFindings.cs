using Podkop.Findings.Domain;
using Podkop.Shared.Infrastructure;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     The sample findings the Development database is seeded with (issue #67). Roughly two
///     thirds of the findings are promoted; some have no thumbnail. Since issue #16 the seeded
///     comment threads are the authority for comment counts, and since issue #67 the two sides of
///     that pact generate in different processes — the migration worker writes the findings to
///     PostgreSQL while the API host still seeds the discussions in memory — so nothing can line
///     them up after the fact. Generation is therefore deterministic: ids derive from the
///     finding's index, every draw comes from a per-finding stream, and the comment count is the
///     shared <see cref="SampleDiscussions" /> plan's answer for that id. Any two calls, in any
///     process, produce the same findings.
/// </summary>
public static class SampleFindings
{
    /// <summary>
    ///     The stub current user, mirroring <c>StubCurrentUser</c> in the composition root — the
    ///     identity whose own vote the app highlights, so the seed has to place it on purpose.
    /// </summary>
    private const string StubUser = "ada_lovelace";

    public static IReadOnlyList<Finding> Generate(int count = 30)
    {
        var now = DateTimeOffset.UtcNow;
        var authorsWithoutStub = SampleData.Authors.Remove(StubUser);

        return Enumerable.Range(1, count).Select(index =>
        {
            // One stream per finding, seeded by its index: a draw added to one finding must not
            // reshuffle the ones after it.
            var random = new Random(index);
            var id = IdFor(index);
            var createdAt = now.AddHours(-random.Next(2, 96));
            var promoted = index % 3 != 0;
            var digCount = promoted ? random.Next(50, 150) : random.Next(0, 49);
            var buryCount = promoted ? random.Next(0, 50) : random.Next(50, 150);

            var stubVote = StubVoteFor(index, random);
            // Nobody may vote on their own finding, so the stub user does not get to author the
            // ones her vote is due on — otherwise that vote is silently dropped and the mix
            // below degrades from guaranteed to merely likely.
            var author = stubVote is null
                ? SampleData.Authors[random.Next(SampleData.Authors.Length)]
                : authorsWithoutStub[random.Next(authorsWithoutStub.Length)];

            // The crowd excludes her as well: her side is chosen deliberately rather than
            // inherited from wherever she happens to sit in the voter list.
            var voters = SampleData.Authors.Except(new[] { author, StubUser })
                .Concat(SampleData.Voters).ToArray();
            return new Finding(
                id: id,
                title: $"Sample finding {index}",
                description: string.Join(" ",
                    random.GetItems(SampleData.Lines.AsSpan(), random.Next(1, 4))),
                source: new Uri(
                    $"https://{SampleData.Hosts[random.Next(SampleData.Hosts.Length)]}/article/{index}"),
                thumbnail: index % 5 == 0 ? null : new Uri($"https://picsum.photos/id/{index * 10}/220/142"),
                author: author,
                tags: random.GetItems(SampleData.Tags.AsSpan(), random.Next(1, 4)).Distinct().ToArray(),
                createdAt: createdAt,
                promotedAt: promoted ? createdAt.AddHours(random.Next(1, 24)) : null,
                commentCount: SampleDiscussions.CommentCountFor(id),
                votes: SeedVotes(random, digCount, voters, buryCount, promoted, stubVote));
        }).ToArray();
    }

    /// <summary>
    ///     The finding's identity as a pure function of its index — the anchor the whole
    ///     deterministic seed hangs off, and legible in a database row to boot: the index sits in
    ///     the id's last twelve digits.
    /// </summary>
    private static Guid IdFor(int index) => new($"00000000-0000-0000-0001-{index:D12}");

    /// <summary>
    ///     The stub user's own vote on a finding, or <c>null</c> where she has not voted. Fixed by
    ///     position rather than drawn at random: the Main Page carries only promoted findings, so a
    ///     mix that is merely likely across the whole seed can still leave the feed showing digs
    ///     alone (issue #15). Every fourth finding takes a bury and every fourth a dig, which lands
    ///     both sides on the promoted two thirds several times over, while the remaining half stays
    ///     unvoted so the highlight reads as scattered rather than blanket.
    /// </summary>
    private static FindingVote? StubVoteFor(int index, Random random) =>
        (index % 4) switch
        {
            1 => new FindingVote(FindingVoteSide.Bury, (BuryReason)random.Next(0, 5)),
            2 => new FindingVote(FindingVoteSide.Dig, null),
            _ => null
        };

    private static Dictionary<string, FindingVote> SeedVotes(Random random, int digCount, string[] voters,
        int buryCount, bool promoted, FindingVote? stubVote)
    {
        Dictionary<string, FindingVote> GetDigVotes(int starting, int count) =>
            Enumerable.Range(starting, count).Select(i => voters[i])
                .ToDictionary(voter => voter, _ => new FindingVote(FindingVoteSide.Dig, null));

        Dictionary<string, FindingVote> GetBuryVotes(int starting, int count) =>
            Enumerable.Range(starting, count).Select(i => voters[i])
                .ToDictionary(voter => voter,
                    _ => new FindingVote(FindingVoteSide.Bury, (BuryReason)random.Next(0, 5)));

        var votes = (promoted
                ? GetDigVotes(0, digCount).Concat(GetBuryVotes(digCount, buryCount))
                : GetBuryVotes(0, buryCount)
                    .Concat(GetDigVotes(buryCount, digCount)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Added on top of the crowd, never colliding with it: `voters` has the stub user removed.
        if (stubVote is not null) votes[StubUser] = stubVote;

        return votes;
    }
}
