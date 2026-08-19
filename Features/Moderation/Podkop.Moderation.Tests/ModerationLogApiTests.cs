using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Moderation.Tests;

/// <summary>
///     The Moderation Log through the HTTP seam (issue #35): GET /api/moderation/log lists
///     every verdict — the Verdict IS the log entry — newest first, as a flat unpaginated
///     array serialized with the Web defaults (<c>actor</c>, <c>targetKind</c>,
///     <c>targetId</c>, <c>verdict</c>, <c>issuedAt</c>, <c>resolvedReportCount</c>), and the
///     whole surface is refused to anyone but a Moderator. The world outside the slice enters
///     only through its own ports (ADR 0003), stubbed here.
/// </summary>
public class ModerationLogApiTests
{
    private const string StubUser = "ada_lovelace";

    private static readonly Guid ReportedFindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid SecondFindingId = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");
    private static readonly Guid ReportedCommentId = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");

    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    /// <summary>The instant the clock starts at; seeded reports predate it.</summary>
    private static readonly DateTimeOffset Now = At("2026-07-10T12:00:00Z");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Hosts the composition root with the slice's ports pinned: who moderates, the judged
    ///     contents' facts (the dismissal round-trips need them), a controllable clock — the
    ///     newest-first specs advance it between dismissals so instants stay distinct — and the
    ///     report and verdict repositories.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(
        InMemoryReportRepository reports,
        InMemoryVerdictRepository verdicts,
        bool actingUserIsModerator = true,
        FakeTimeProvider? clock = null)
    {
        string[] moderators = actingUserIsModerator ? [StubUser] : [];
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(clock ?? new FakeTimeProvider(Now));
                services.AddSingleton<IModeratorLookup>(new StubModeratorLookup(moderators));
                services.AddSingleton<ICaseContentLookup>(new StubCaseContentLookup(
                    (ReportTargetKind.Finding, ReportedFindingId,
                        new CaseContent("grace_hopper", "A finding under scrutiny", ReportedFindingId)),
                    (ReportTargetKind.Finding, SecondFindingId,
                        new CaseContent("margaret_h", "Another finding under scrutiny", SecondFindingId))));
                services.AddSingleton<IReportRepository>(reports);
                services.AddSingleton<IVerdictRepository>(verdicts);
            }));
    }

    private static Report ReportBy(string reporter, ReportTargetKind kind, Guid targetId, string filedAtIso) =>
        new(Guid.CreateVersion7(), reporter, kind, targetId, SpamPointId,
            statuteVersion: 2, note: null, At(filedAtIso));

    private static Verdict DismissalBy(string actor, ReportTargetKind kind, Guid targetId,
        string issuedAtIso, params Guid[] resolvedReportIds) =>
        new(Guid.CreateVersion7(), actor, kind, targetId, VerdictKind.Dismissed, At(issuedAtIso),
            resolvedReportIds);

    private static Task<HttpResponseMessage> Dismiss(HttpClient client, string targetKind, Guid targetId) =>
        client.PostAsJsonAsync($"/api/moderation/cases/{targetKind}/{targetId}/verdict",
            new { verdict = "Dismissed" });

    private static async Task<List<LogEntryResponse>> GetLogEntries(HttpClient client)
    {
        var response = await client.GetAsync("/api/moderation/log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<LogEntryResponse>>();
        Assert.NotNull(entries);
        return entries;
    }

    [Fact]
    public async Task The_log_records_a_dismissal()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T11:00:00Z"),
            ReportBy("nick_chapsas", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T12:00:00Z"),
        ]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var dismissed = await Dismiss(client, "Finding", ReportedFindingId);
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);

        var entry = Assert.Single(await GetLogEntries(client));
        Assert.Equal(StubUser, entry.Actor);
        Assert.Equal("Finding", entry.TargetKind);
        Assert.Equal(ReportedFindingId, entry.TargetId);
        Assert.Equal("Dismissed", entry.Verdict);
        Assert.Equal(Now, entry.IssuedAt);
        Assert.Equal(3, entry.ResolvedReportCount);
    }

    [Fact]
    public async Task The_log_reads_newest_first()
    {
        // Another moderator's verdict sits between this session's two dismissals.
        // Expected newest-first: SecondFinding (14:00), the seeded comment verdict (13:00),
        // ReportedFinding (12:00). Store/insertion order (seeded, Reported, Second), its
        // reverse, and id order all answer a different sequence, so the ordering is falsifiable.
        var clock = new FakeTimeProvider(Now);
        var seeded = DismissalBy("grace_hopper", ReportTargetKind.Comment, ReportedCommentId,
            "2026-07-10T13:00:00Z", Guid.CreateVersion7());
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, SecondFindingId, "2026-07-02T11:00:00Z"),
        ]), new InMemoryVerdictRepository([seeded]), clock: clock);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent,
            (await Dismiss(client, "Finding", ReportedFindingId)).StatusCode);
        clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(HttpStatusCode.NoContent,
            (await Dismiss(client, "Finding", SecondFindingId)).StatusCode);

        var entries = await GetLogEntries(client);

        Assert.Equal([SecondFindingId, ReportedCommentId, ReportedFindingId],
            entries.Select(e => e.TargetId));
        // Strictly descending instants — no ties to hide a wrong sort behind.
        Assert.Equal(entries.Select(e => e.IssuedAt).OrderByDescending(at => at),
            entries.Select(e => e.IssuedAt));
        Assert.Distinct(entries.Select(e => e.IssuedAt));
    }

    [Fact]
    public async Task A_seeded_verdict_lists_with_its_facts()
    {
        // Verdicts stored before this session — the sample seed's case — list like live ones.
        var seeded = DismissalBy("grace_hopper", ReportTargetKind.Comment, ReportedCommentId,
            "2026-07-01T09:00:00Z", Guid.CreateVersion7(), Guid.CreateVersion7());
        using var factory = CreateFactory(new InMemoryReportRepository([]),
            new InMemoryVerdictRepository([seeded]));
        using var client = factory.CreateClient();

        var entry = Assert.Single(await GetLogEntries(client));
        Assert.Equal("grace_hopper", entry.Actor);
        Assert.Equal("Comment", entry.TargetKind);
        Assert.Equal(ReportedCommentId, entry.TargetId);
        Assert.Equal("Dismissed", entry.Verdict);
        Assert.Equal(At("2026-07-01T09:00:00Z"), entry.IssuedAt);
        Assert.Equal(2, entry.ResolvedReportCount);
    }

    [Fact]
    public async Task An_empty_log_answers_an_empty_list()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        Assert.Empty(await GetLogEntries(client));
    }

    [Fact]
    public async Task A_member_is_refused_the_log()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]),
            new InMemoryVerdictRepository([]), actingUserIsModerator: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/moderation/log");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:moderators-only", problem!.Type);
    }

    private sealed record LogEntryResponse(
        string Actor,
        string TargetKind,
        Guid TargetId,
        string Verdict,
        DateTimeOffset IssuedAt,
        int ResolvedReportCount);

    private sealed record ProblemResponse(string Type);
}
