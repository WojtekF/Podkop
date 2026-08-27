using Podkop.FindingComments.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The sample discussions' cross-process pact (issue #68): the migration worker persists the
///     generated comments in its own process, while the API host's <c>SampleSeed</c> regenerates
///     them to project report targets for the still-in-memory Moderation slice — the seeded
///     reports cite comment ids, so both processes must arrive at the very same rows. That is
///     only possible when generation is a pure function of the finding ids it is handed: two runs
///     over the same ids must agree on every identity fact a another slice can reference — the
///     comment ids, whose finding and parent they hang off, and who wrote them. Before issue #68
///     the ids could stay fresh per process because nothing outside the process referenced them;
///     that freedom is gone.
/// </summary>
public class SampleFindingCommentsDeterminismTests
{
    private static readonly Guid[] FindingIds =
    [
        Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666"),
        Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666"),
        Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666"),
    ];

    [Fact]
    public void Two_generations_over_the_same_findings_agree_on_every_referencable_identity_fact()
    {
        var first = SampleFindingComments.GenerateFor(FindingIds);
        var second = SampleFindingComments.GenerateFor(FindingIds);

        // The pact must be exercised for real, not satisfied by a world of zeroes.
        Assert.NotEmpty(first);

        Assert.Equal(
            first.Select(comment => (comment.Id, comment.FindingId, comment.ParentCommentId, comment.Author)),
            second.Select(comment => (comment.Id, comment.FindingId, comment.ParentCommentId, comment.Author)));
    }
}
