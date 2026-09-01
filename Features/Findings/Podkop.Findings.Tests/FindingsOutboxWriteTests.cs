using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Contracts;

namespace Podkop.Findings.Tests;

/// <summary>
///     The write half of the transactional outbox for this slice (issue #77, ADR 0014): the tag
///     announcements a finding raises become rows of the very save that makes the finding's own
///     state durable, so the index can never be told about a change that did not land, nor miss
///     one that did. The specs run against real PostgreSQL because atomicity is the whole claim.
///     Announcements are asserted by reading the table back, never through a publisher: nothing is
///     published at this stage, and the processor that eventually does is a separate concern.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class FindingsOutboxWriteTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-7777-4222-8333-444455556666");

    /// <summary>Pinned rather than inherited from the test run, so the stamp is falsifiable.</summary>
    private static readonly DateTimeOffset Now = At("2026-08-28T09:15:00Z");

    private readonly FakeTimeProvider _clock = new(Now);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(params string[] tags) =>
        new(
            id: FindingId,
            title: "A tagged finding",
            description: "A tagged finding — description",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: tags,
            createdAt: At("2026-07-01T06:00:00Z"),
            promotedAt: null,
            commentCount: 0,
            votes: null);

    private async Task GivenFindings(params Finding[] findings)
    {
        await using var context = database.CreateDbContext();
        context.Findings.AddRange(findings);
        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     One use case's worth of work through a context that carries the outbox interceptor —
    ///     the save is the seam under test, so the specs commit through the context itself.
    /// </summary>
    private async Task InOneUseCase(Func<FindingsDbContext, Task> useCase)
    {
        await using var context = database.CreateDbContextWithOutbox(
            new FindingsContractEventTranslator(), _clock);
        await useCase(context);
    }

    /// <summary>Everything the slice has announced, read back from its own schema.</summary>
    private async Task<IReadOnlyList<OutboxMessage>> AnnouncedAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt).ToListAsync();
    }

    [Fact]
    public async Task A_committed_tag_change_announces_the_resulting_set()
    {
        await GivenFindings(CreateFinding("dotnet"));

        await InOneUseCase(async context =>
        {
            var finding = await context.Findings.FirstAsync(f => f.Id == FindingId);
            finding.SetTags(["dotnet", "aspire"]);
            await context.SaveChangesAsync();
        });

        var row = Assert.Single(await AnnouncedAsync());

        // Loose on how the type is spelled, strict on what it must identify: the processor
        // resolves rows without knowing which slice wrote them.
        Assert.Contains(typeof(TaggedContentAnnounced).FullName!, row.Type);

        // Case-insensitive on purpose — the round trip is the claim, not a casing convention.
        var announced = JsonSerializer.Deserialize<TaggedContentAnnounced>(
            row.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(announced);
        Assert.Equal(FindingId, announced.ContentId);
        Assert.Equal(TaggedContentTypes.Finding, announced.ContentType);
        Assert.Equal(["dotnet", "aspire"], announced.Tags);
        Assert.Equal(At("2026-07-01T06:00:00Z"), announced.CreatedAt);
    }

    [Fact]
    public async Task A_committed_removal_announces_that_the_finding_is_gone()
    {
        await GivenFindings(CreateFinding("dotnet"));

        await InOneUseCase(async context =>
        {
            var finding = await context.Findings.FirstAsync(f => f.Id == FindingId);
            finding.Remove();
            await context.SaveChangesAsync();
        });

        var row = Assert.Single(await AnnouncedAsync());
        Assert.Contains(typeof(TaggedContentRemoved).FullName!, row.Type);
    }

    [Fact]
    public async Task An_announcement_is_stamped_from_the_supplied_clock_and_waits_to_be_published()
    {
        await GivenFindings(CreateFinding("dotnet"));

        await InOneUseCase(async context =>
        {
            var finding = await context.Findings.FirstAsync(f => f.Id == FindingId);
            finding.SetTags(["aspire"]);
            await context.SaveChangesAsync();
        });

        var row = Assert.Single(await AnnouncedAsync());
        Assert.Equal(Now, row.OccurredAt);

        // The write side only ever leaves work for the processor; a row that arrives already
        // marked processed would never be published at all.
        Assert.Null(row.ProcessedAt);
    }

    [Fact]
    public async Task Committing_a_finding_that_raised_nothing_announces_nothing()
    {
        // Translation, not fabrication: the row exists because the aggregate raised something,
        // never merely because a finding was written. This is also what keeps the migration
        // worker's seed silent — it constructs findings raw.
        // Holds trivially while the translator is unimplemented (nothing is raised, so it is
        // never called); kept as a standing guard against the opposite failure.
        await InOneUseCase(async context =>
        {
            context.Findings.Add(CreateFinding("dotnet"));
            await context.SaveChangesAsync();
        });

        Assert.Empty(await AnnouncedAsync());
    }

    [Fact]
    public async Task A_committed_vote_announces_nothing()
    {
        // Votes are the slice's own business, and the tag index deliberately carries no scores
        // (ADR 0011) — a commit that only records a vote must leave the outbox empty. Holds
        // trivially today for the same reason as the spec above, and stands for the same reason.
        await GivenFindings(CreateFinding("dotnet"));

        await InOneUseCase(async context =>
        {
            var finding = await context.Findings.FirstAsync(f => f.Id == FindingId);
            Assert.Equal(DigBuryOutcome.Applied, finding.SetVote("ada_lovelace", FindingVoteSide.Dig, null));
            await context.SaveChangesAsync();
        });

        Assert.Empty(await AnnouncedAsync());
    }

    [Fact]
    public async Task A_second_commit_does_not_announce_the_tag_change_again()
    {
        // A drained aggregate has nothing left to announce; otherwise every later save in the
        // same scope would duplicate the row and the index would hear the change twice.
        await GivenFindings(CreateFinding("dotnet"));

        await InOneUseCase(async context =>
        {
            var finding = await context.Findings.FirstAsync(f => f.Id == FindingId);
            finding.SetTags(["aspire"]);
            await context.SaveChangesAsync();
            await context.SaveChangesAsync();
        });

        Assert.Single(await AnnouncedAsync());
    }
}
