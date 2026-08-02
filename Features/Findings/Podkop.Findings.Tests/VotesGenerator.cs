using Podkop.Findings.Domain;

namespace Podkop.Findings.Tests;

public static class VotesGenerator
{
    public static Dictionary<string, FindingVote> Generate(int digCount, int buryCount)
    {
        var digVotes = Enumerable.Range(0, digCount)
            .ToDictionary(
                i => i.ToString(),
                i => new FindingVote(FindingVoteSide.Dig, null));
        var buryVotes = Enumerable.Range(digCount, buryCount)
            .ToDictionary(
                i => i.ToString(),
                i => new FindingVote(FindingVoteSide.Bury,
                    (BuryReason)Random.Shared.Next(0, 5)));
        return digVotes.Concat(
            buryVotes).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
