using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The database seed's own rule (issue #68, mirroring the Findings seed): a fresh database
///     receives every given comment, and a run that finds records already there leaves them
///     exactly as it found them — the orchestrated database keeps its data across restarts and
///     the worker seeds on every start, so repeated runs must not make the population grow or
///     change. Runs on the real PostgreSQL fixture: the seed must work on exactly the engine and
///     schema the worker writes to.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class FindingCommentsDatabaseSeedTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Comment CreateComment(string text) =>
        new(
            Guid.NewGuid(),
            Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666"),
            null,
            "grace_hopper",
            text,
            At("2026-07-08T10:00:00Z"));

    private async Task Seeded(IReadOnlyList<Comment> comments)
    {
        await using var context = database.CreateDbContext();
        await FindingCommentsSeed.SeedAsync(context, comments, CancellationToken.None);
    }

    private async Task<(Guid Id, string Text)[]> Population()
    {
        await using var context = database.CreateDbContext();
        return (await context.Comments
                .AsNoTracking()
                .Select(comment => new { comment.Id, comment.Text })
                .ToArrayAsync())
            .Select(comment => (comment.Id, comment.Text))
            .OrderBy(comment => comment.Id)
            .ToArray();
    }

    private static (Guid Id, string Text)[] Expected(IEnumerable<Comment> comments) =>
        comments
            .Select(comment => (comment.Id, comment.Text))
            .OrderBy(comment => comment.Id)
            .ToArray();

    [Fact]
    public async Task A_fresh_database_receives_every_given_comment()
    {
        var comments = new[]
        {
            CreateComment("A first take."),
            CreateComment("A second take."),
        };

        await Seeded(comments);

        Assert.Equal(Expected(comments), await Population());
    }

    [Fact]
    public async Task A_second_run_leaves_the_population_exactly_as_it_found_it()
    {
        var comments = new[] { CreateComment("A take worth keeping once.") };

        await Seeded(comments);
        await Seeded(comments);

        Assert.Equal(Expected(comments), await Population());
    }

    /// <summary>
    ///     The skip is decided by the population being there at all, not by comparing it against
    ///     the sample set: a database that already holds comments keeps exactly those, even when
    ///     the sample vocabulary has moved on since — the behaviour a kept data volume shows
    ///     after the generator changes.
    /// </summary>
    [Fact]
    public async Task A_run_that_finds_comments_keeps_them_even_when_the_samples_have_changed()
    {
        var alreadyThere = new[] { CreateComment("Someone from an older run") };
        await Seeded(alreadyThere);

        await Seeded([CreateComment("The new sample vocabulary")]);

        Assert.Equal(Expected(alreadyThere), await Population());
    }
}
