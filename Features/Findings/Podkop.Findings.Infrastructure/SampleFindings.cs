using Podkop.Findings.Domain;
using Podkop.Shared.Infrastructure;

namespace Podkop.Findings.Infrastructure;

/// <summary>
///     Development seed data until PostgreSQL persistence lands. Roughly two thirds of the
///     findings are promoted; some have no thumbnail. Since issue #16 the seeded comment threads
///     are the authority for comment counts: a finding's CommentCount may no longer be invented
///     here — the composition root's SampleSeed lines this generator up with the seeded discussion.
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
            var createdAt = now.AddHours(-Random.Shared.Next(2, 96));
            var promoted = index % 3 != 0;
            var digCount = promoted ? Random.Shared.Next(50, 150) : Random.Shared.Next(0, 49);
            var buryCount = promoted ? Random.Shared.Next(0, 50) : Random.Shared.Next(50, 150);

            var stubVote = StubVoteFor(index);
            // Nobody may vote on their own finding, so the stub user does not get to author the
            // ones her vote is due on — otherwise that vote is silently dropped and the mix
            // below degrades from guaranteed to merely likely.
            var author = stubVote is null
                ? SampleData.Authors[Random.Shared.Next(SampleData.Authors.Length)]
                : authorsWithoutStub[Random.Shared.Next(authorsWithoutStub.Length)];

            // The crowd excludes her as well: her side is chosen deliberately rather than
            // inherited from wherever she happens to sit in the voter list.
            var voters = SampleData.Authors.Except(new[] { author, StubUser })
                .Concat(SampleData.Voters).ToArray();
            return new Finding(
                Guid.NewGuid(),
                $"Sample finding {index}",
                string.Join(" ", Random.Shared.GetItems(SampleData.Lines.AsSpan(), Random.Shared.Next(1, 4))),
                new Uri($"https://{SampleData.Hosts[Random.Shared.Next(SampleData.Hosts.Length)]}/article/{index}"),
                index % 5 == 0 ? null : new Uri($"https://picsum.photos/id/{index * 10}/220/142"),
                author,
                Random.Shared.GetItems(SampleData.Tags.AsSpan(), Random.Shared.Next(1, 4)).Distinct().ToArray(),
                createdAt,
                promoted ? createdAt.AddHours(Random.Shared.Next(1, 24)) : null,
                Random.Shared.Next(0, 250),
                SeedVotes(digCount, voters, buryCount, promoted, stubVote));
        }).ToArray();
    }

    /// <summary>
    ///     The stub user's own vote on a finding, or <c>null</c> where she has not voted. Fixed by
    ///     position rather than drawn at random: the Main Page carries only promoted findings, so a
    ///     mix that is merely likely across the whole seed can still leave the feed showing digs
    ///     alone (issue #15). Every fourth finding takes a bury and every fourth a dig, which lands
    ///     both sides on the promoted two thirds several times over, while the remaining half stays
    ///     unvoted so the highlight reads as scattered rather than blanket.
    /// </summary>
    private static FindingVote? StubVoteFor(int index)
    {
        return (index % 4) switch
        {
            1 => new FindingVote(FindingVoteSide.Bury, (BuryReason)Random.Shared.Next(0, 5)),
            2 => new FindingVote(FindingVoteSide.Dig, null),
            _ => null
        };
    }

    private static Dictionary<string, FindingVote> SeedVotes(int digCount, string[] voters, int buryCount,
        bool promoted, FindingVote? stubVote)
    {
        Dictionary<string, FindingVote> GetDigVotes(int starting, int count)
        {
            return Enumerable.Range(starting, count).Select(i => voters[i])
                .ToDictionary(voter => voter, _ => new FindingVote(FindingVoteSide.Dig, null));
        }

        Dictionary<string, FindingVote> GetBuryVotes(int starting, int count)
        {
            return Enumerable.Range(starting, count).Select(i => voters[i])
                .ToDictionary(voter => voter,
                    _ => new FindingVote(FindingVoteSide.Bury, (BuryReason)Random.Shared.Next(0, 5)));
        }

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
