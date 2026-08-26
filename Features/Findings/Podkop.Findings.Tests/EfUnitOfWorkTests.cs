using System.Globalization;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

/// <summary>
///     The commit seam against the live database (issue #96): an aggregate the repository hands
///     back is change-tracked, a use case mutates it through its domain methods, and only the
///     unit of work's one explicit commit makes the mutation durable — a vote set, switched or
///     withdrawn in one context, or a comment counted on it, is then what the next context reads,
///     while a mutation never committed is gone with its context. These are the durability round
///     trips issue #67 proved through <c>SaveAsync</c>, moved to the seam that replaced it.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class EfUnitOfWorkTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(
        Guid id,
        string title,
        int commentCount = 0,
        IReadOnlyDictionary<string, FindingVote>? votes = null)
        => new(
            id: id,
            title: title,
            description: $"{title} — description",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: ["dotnet"],
            createdAt: At("2026-07-01T06:00:00Z"),
            promotedAt: At("2026-07-08T09:30:00Z"),
            commentCount: commentCount,
            votes: votes);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    private async Task<Finding?> LookedUp(Guid id)
    {
        await using var context = database.CreateDbContext();
        return await new EfFindingRepository(context).GetByIdAsync(id, CancellationToken.None);
    }

    /// <summary>
    ///     One use case's worth of work: load the finding through the repository, mutate it, and
    ///     — when the use case commits — flush the same context through the unit of work, the way
    ///     a handler's request scope shares one context between the two.
    /// </summary>
    private async Task InOneUseCase(Guid id, Action<Finding> mutate, bool committed = true)
    {
        await using var context = database.CreateDbContext();
        var finding = await new EfFindingRepository(context).GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(finding);
        mutate(finding);
        if (committed) await new EfUnitOfWork(context).CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_committed_vote_is_what_the_next_context_reads()
    {
        var id = Guid.Parse("0d4f9a3e-4444-4222-8333-444455556666");
        await GivenFindings(CreateFinding(
            id,
            "A finding worth judging",
            votes: new Dictionary<string, FindingVote>
            {
                ["linus_t"] = new(FindingVoteSide.Dig, null)
            }));

        await InOneUseCase(id, finding => Assert.Equal(DigBuryOutcome.Applied,
            finding.SetVote(StubUser, FindingVoteSide.Dig, null)));

        var reloaded = await LookedUp(id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.DigCount);
        Assert.Equal(FindingVoteSide.Dig, reloaded.VoteBy(StubUser));
    }

    [Fact]
    public async Task A_committed_side_switch_moves_the_vote_not_copies_it()
    {
        var id = Guid.Parse("0d4f9a3e-5555-4222-8333-444455556666");
        await GivenFindings(CreateFinding(
            id,
            "A finding worth judging",
            votes: new Dictionary<string, FindingVote>
            {
                [StubUser] = new(FindingVoteSide.Dig, null)
            }));

        await InOneUseCase(id,
            finding => finding.SetVote(StubUser, FindingVoteSide.Bury, BuryReason.Duplicate));

        var reloaded = await LookedUp(id);
        Assert.NotNull(reloaded);
        // The dig is gone and the bury stands in its place — a commit that appended instead of
        // moving would read one of each.
        Assert.Equal(0, reloaded.DigCount);
        Assert.Equal(1, reloaded.BuryCount);
        Assert.Equal(FindingVoteSide.Bury, reloaded.VoteBy(StubUser));
    }

    [Fact]
    public async Task A_committed_withdrawal_is_gone_for_the_next_context()
    {
        var id = Guid.Parse("0d4f9a3e-6666-4222-8333-444455556666");
        await GivenFindings(CreateFinding(
            id,
            "A finding worth judging",
            votes: new Dictionary<string, FindingVote>
            {
                ["linus_t"] = new(FindingVoteSide.Dig, null),
                [StubUser] = new(FindingVoteSide.Dig, null)
            }));

        await InOneUseCase(id, finding => finding.WithdrawVote(StubUser));

        var reloaded = await LookedUp(id);
        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded.DigCount);
        Assert.Null(reloaded.VoteBy(StubUser));
    }

    [Fact]
    public async Task A_committed_comment_count_is_what_the_next_context_reads()
    {
        var id = Guid.Parse("0d4f9a3e-7777-4222-8333-444455556666");
        await GivenFindings(CreateFinding(id, "A discussed finding", commentCount: 7));

        await InOneUseCase(id, finding => finding.IncrementCommentCount());

        var reloaded = await LookedUp(id);
        Assert.Equal(8, reloaded!.CommentCount);
    }

    [Fact]
    public async Task An_uncommitted_mutation_is_invisible_to_the_next_context()
    {
        // The other half of the seam's contract: since issue #96 nothing persists on its own —
        // a use case that mutates its loaded aggregate but never commits has changed nothing.
        var id = Guid.Parse("0d4f9a3e-8888-4222-8333-444455556666");
        await GivenFindings(CreateFinding(id, "A finding worth judging", commentCount: 7));

        await InOneUseCase(id, finding =>
        {
            finding.SetVote(StubUser, FindingVoteSide.Dig, null);
            finding.IncrementCommentCount();
        }, committed: false);

        var reloaded = await LookedUp(id);
        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.DigCount);
        Assert.Null(reloaded.VoteBy(StubUser));
        Assert.Equal(7, reloaded.CommentCount);
    }
}
