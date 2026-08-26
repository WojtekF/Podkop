using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Findings.Tests;

/// <summary>
///     The database seed's own rule (issue #67): a fresh database receives every given finding,
///     and a run that finds records already there leaves them exactly as it found them — the
///     orchestrated database keeps its data across restarts and the worker seeds on every start,
///     so repeated runs must not make the population grow or change. Unlike the Users slice's
///     seed specs, these run on the real PostgreSQL fixture: the findings model is the first
///     non-trivial aggregate to persist, and the seed must work on exactly the engine and schema
///     the worker writes to.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingsDatabaseSeedTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(string title, DateTimeOffset? promotedAt) =>
        new(
            Guid.NewGuid(),
            title,
            $"{title} — description",
            new Uri("https://example.org/articles/1"),
            null,
            "grace_hopper",
            ["dotnet"],
            At("2026-07-01T06:00:00Z"),
            promotedAt,
            0);

    private async Task Seeded(IReadOnlyList<Finding> findings)
    {
        await using var context = database.CreateDbContext();
        await FindingsSeed.SeedAsync(context, findings, CancellationToken.None);
    }

    private async Task<(Guid Id, string Title)[]> Population()
    {
        await using var context = database.CreateDbContext();
        return (await context.Findings
                .AsNoTracking()
                .Select(finding => new { finding.Id, finding.Title })
                .ToArrayAsync())
            .Select(finding => (finding.Id, finding.Title))
            .OrderBy(finding => finding.Id)
            .ToArray();
    }

    private static (Guid Id, string Title)[] Expected(IEnumerable<Finding> findings) =>
        findings
            .Select(finding => (finding.Id, finding.Title))
            .OrderBy(finding => finding.Id)
            .ToArray();

    [Fact]
    public async Task A_fresh_database_receives_every_given_finding()
    {
        var findings = new[]
        {
            CreateFinding("Promoted sample", At("2026-07-08T10:00:00Z")),
            CreateFinding("Upcoming sample", null),
        };

        await Seeded(findings);

        Assert.Equal(Expected(findings), await Population());
    }

    [Fact]
    public async Task A_second_run_leaves_the_population_exactly_as_it_found_it()
    {
        var findings = new[] { CreateFinding("Promoted sample", At("2026-07-08T10:00:00Z")) };

        await Seeded(findings);
        await Seeded(findings);

        Assert.Equal(Expected(findings), await Population());
    }

    /// <summary>
    ///     The skip is decided by the population being there at all, not by comparing it against
    ///     the sample set: a database that already holds findings keeps exactly those, even when
    ///     the sample vocabulary has moved on since. Spelled out because it is the behaviour a
    ///     kept data volume shows after the generator changes — the seed will not reconcile it,
    ///     and the volume has to go for the new population to land.
    /// </summary>
    [Fact]
    public async Task A_run_that_finds_findings_keeps_them_even_when_the_samples_have_changed()
    {
        var alreadyThere = new[] { CreateFinding("Someone from an older run", At("2026-07-08T10:00:00Z")) };
        await Seeded(alreadyThere);

        await Seeded([CreateFinding("The new sample vocabulary", At("2026-07-09T10:00:00Z"))]);

        Assert.Equal(Expected(alreadyThere), await Population());
    }
}
