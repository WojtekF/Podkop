using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Tests;

public static class VotesGenerator
{
    public static Dictionary<string, VoteDirection> Generate(int downvotes, int upvotes)
    {
        var upvotesDictionary = Enumerable.Range(1, upvotes)
            .ToDictionary(key => key.ToString(), value => VoteDirection.Up);
        var downvotesDictionary = Enumerable.Range(upvotes + 1, downvotes)
            .ToDictionary(key => key.ToString(), value => VoteDirection.Down);

        return upvotesDictionary.Concat(downvotesDictionary).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
