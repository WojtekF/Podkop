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
///     Dismissing a case through the HTTP seam (issue #35): POST verdict on a case's target
///     rules the open case unfounded — 204 — storing one Verdict that resolves every pending
///     report at once and removes the case from the queue; the target may then be reported
///     afresh, by its old reporters included, as a brand-new case. Refusals: members are
///     refused wholesale, moderators for cases about their own content, and a target without
///     pending reports has no case — never reported and already dismissed answer the same 404.
///     The world outside the slice enters only through its own ports (ADR 0003), stubbed here.
/// </summary>
public class DismissCaseApiTests
{
    private const string StubUser = "ada_lovelace";

    private static readonly Guid ReportedFindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid OwnFindingId = Guid.Parse("0d4f9a3e-5555-4222-8333-444455556666");
    private static readonly Guid ReportedCommentId = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");

    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");

    /// <summary>The current Statute as the port answers it — the re-filing spec cites a reportable point.</summary>
    private static readonly CurrentStatute CurrentStatuteV2 = new(2, [SpamPointId, HatePointId]);

    /// <summary>The dismissal instant every spec pins; seeded reports are always filed before it.</summary>
    private static readonly DateTimeOffset Now = At("2026-07-10T12:00:00Z");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Hosts the composition root with every collaborator the slice sees pinned through its
    ///     own ports: the clock, who moderates, the reported contents' facts (one authored by
    ///     the acting stub user, for the own-case refusal), the finding as a filing target (for
    ///     the fresh-case specs), and the report and verdict repositories the specs keep
    ///     references to.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(
        InMemoryReportRepository reports,
        InMemoryVerdictRepository verdicts,
        bool actingUserIsModerator = true)
    {
        string[] moderators = actingUserIsModerator ? [StubUser] : [];
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                services.AddSingleton<IModeratorLookup>(new StubModeratorLookup(moderators));
                services.AddSingleton<ICaseContentLookup>(new StubCaseContentLookup(
                    (ReportTargetKind.Finding, ReportedFindingId,
                        new CaseContent("grace_hopper", "A finding under scrutiny", ReportedFindingId)),
                    (ReportTargetKind.Finding, OwnFindingId,
                        new CaseContent(StubUser, "The moderator's own finding", OwnFindingId)),
                    (ReportTargetKind.Comment, ReportedCommentId,
                        new CaseContent("linus_t", "A comment under scrutiny.", ReportedFindingId))));
                services.AddSingleton<IReportTargetLookup>(new StubReportTargetLookup(
                    (ReportTargetKind.Finding, new ReportTarget(ReportedFindingId, "grace_hopper"))));
                services.AddSingleton<IStatuteLookup>(new StubStatuteLookup(CurrentStatuteV2,
                    (SpamPointId, 2, new CitedPoint(2, 1, "Do not post spam. (v2)")),
                    (HatePointId, 2, new CitedPoint(2, 3, "Do not post hateful content."))));
                services.AddSingleton<IReportRepository>(reports);
                services.AddSingleton<IVerdictRepository>(verdicts);
            }));
    }

    private static Report ReportBy(string reporter, ReportTargetKind kind, Guid targetId,
        string filedAtIso, Guid? pointId = null) =>
        new(Guid.CreateVersion7(), reporter, kind, targetId, pointId ?? SpamPointId,
            statuteVersion: 2, note: null, At(filedAtIso));

    private static Task<HttpResponseMessage> Dismiss(HttpClient client, string targetKind, Guid targetId) =>
        client.PostAsJsonAsync($"/api/moderation/cases/{targetKind}/{targetId}/verdict",
            new { verdict = "Dismissed" });

    private static async Task<List<CaseResponse>> GetQueueCases(HttpClient client)
    {
        var response = await client.GetAsync("/api/moderation/cases");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cases = await response.Content.ReadFromJsonAsync<List<CaseResponse>>();
        Assert.NotNull(cases);
        return cases;
    }

    [Fact]
    public async Task Dismissing_a_case_answers_204_and_its_case_leaves_the_queue()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T11:00:00Z",
                HatePointId),
            ReportBy("nick_chapsas", ReportTargetKind.Comment, ReportedCommentId, "2026-07-02T12:00:00Z"),
        ]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await Dismiss(client, "Finding", ReportedFindingId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The dismissed case is gone; the unrelated comment case is untouched.
        var cases = await GetQueueCases(client);
        var remaining = Assert.Single(cases);
        Assert.Equal(ReportedCommentId, remaining.TargetId);
        Assert.Equal(1, remaining.ReportCount);
    }

    [Fact]
    public async Task One_dismissal_resolves_every_pending_report_of_the_case()
    {
        var first = ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z");
        var second = ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T11:00:00Z",
            HatePointId);
        var third = ReportBy("nick_chapsas", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T12:00:00Z");
        var verdicts = new InMemoryVerdictRepository([]);
        using var factory = CreateFactory(new InMemoryReportRepository([first, second, third]), verdicts);
        using var client = factory.CreateClient();

        var response = await Dismiss(client, "Finding", ReportedFindingId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // A three-report case clears with one POST. The stored verdict's facts are asserted
        // through the slice's own repository port; how it reads over HTTP is the log's specs.
        var stored = Assert.Single(
            await verdicts.GetByTargetAsync(ReportTargetKind.Finding, ReportedFindingId, CancellationToken.None));
        Assert.Equal(StubUser, stored.Actor);
        Assert.Equal(VerdictKind.Dismissed, stored.Kind);
        Assert.Equal(Now, stored.IssuedAt);
        Assert.True(new HashSet<Guid> { first.Id, second.Id, third.Id }.SetEquals(stored.ResolvedReportIds),
            "The verdict must capture exactly the pending report ids at dismiss time.");
    }

    [Fact]
    public async Task A_dismissed_case_can_be_reported_again_by_the_same_reporter()
    {
        // The stub user's own earlier report is among the pending ones; its resolution frees
        // them to file the same target again — a fresh case, never a duplicate.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy(StubUser, ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T11:00:00Z",
                HatePointId),
        ]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var dismissed = await Dismiss(client, "Finding", ReportedFindingId);
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);

        var refiled = await client.PostAsJsonAsync($"/api/findings/{ReportedFindingId}/my-report",
            new { statutePointId = SpamPointId, note = (string?)null });
        Assert.Equal(HttpStatusCode.Created, refiled.StatusCode);

        // The queue lists the case again — with ONLY the fresh report, filed at the pinned
        // instant, after the dismissal; the two resolved reports stay gone.
        var cases = await GetQueueCases(client);
        var freshCase = Assert.Single(cases);
        Assert.Equal(ReportedFindingId, freshCase.TargetId);
        var freshReport = Assert.Single(freshCase.Reports);
        Assert.Equal(Now, freshReport.FiledAt);
    }

    [Fact]
    public async Task My_report_resets_to_not_reported_after_the_dismissal()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy(StubUser, ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
        ]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        // The target kind parses case-insensitively on the wire: "finding" is Finding.
        var dismissed = await Dismiss(client, "finding", ReportedFindingId);
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);

        var status = await client.GetFromJsonAsync<MyReportResponse>(
            $"/api/findings/{ReportedFindingId}/my-report");

        Assert.NotNull(status);
        Assert.False(status.Reported);
    }

    [Fact]
    public async Task A_member_may_not_issue_verdicts()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
        ]), new InMemoryVerdictRepository([]), actingUserIsModerator: false);
        using var client = factory.CreateClient();

        var response = await Dismiss(client, "Finding", ReportedFindingId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:moderators-only", problem!.Type);
    }

    [Fact]
    public async Task A_moderator_never_dismisses_a_case_about_their_own_content()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("grace_hopper", ReportTargetKind.Finding, OwnFindingId, "2026-07-02T10:00:00Z"),
        ]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await Dismiss(client, "Finding", OwnFindingId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:own-case", problem!.Type);

        // The refused case stays in the queue, for another moderator to judge.
        var cases = await GetQueueCases(client);
        Assert.Equal(OwnFindingId, Assert.Single(cases).TargetId);
    }

    [Fact]
    public async Task A_never_reported_target_has_no_case()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await Dismiss(client, "Finding", ReportedFindingId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-case", problem!.Type);
    }

    [Fact]
    public async Task A_second_dismissal_finds_no_case_left()
    {
        // Never-reported and already-dismissed are the same state: no pending reports, no case.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, "2026-07-02T10:00:00Z"),
        ]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var first = await Dismiss(client, "Finding", ReportedFindingId);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await Dismiss(client, "Finding", ReportedFindingId);

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-case", problem!.Type);
    }

    [Fact]
    public async Task An_unknown_target_kind_is_a_nonexistent_case()
    {
        // A kind outside the vocabulary is a nonexistent case, not a malformed request —
        // and the vocabulary is names only: the enum's numerals name nothing on the route.
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        foreach (var kind in new[] { "article", "0" })
        {
            var response = await Dismiss(client, kind, ReportedFindingId);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
            Assert.Equal("podkop:problem:unknown-case", problem!.Type);
        }
    }

    [Fact]
    public async Task A_verdict_must_be_named()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/moderation/cases/Finding/{ReportedFindingId}/verdict", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:verdict-required", problem!.Type);
    }

    [Fact]
    public async Task A_blank_verdict_is_not_named()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/moderation/cases/Finding/{ReportedFindingId}/verdict", new { verdict = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:verdict-required", problem!.Type);
    }

    [Fact]
    public async Task An_upheld_verdict_is_not_understood_yet()
    {
        // "Upheld" exists in the domain vocabulary but joins the wire with issue #36.
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/moderation/cases/Finding/{ReportedFindingId}/verdict", new { verdict = "Upheld" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-verdict", problem!.Type);
    }

    [Fact]
    public async Task Verdict_names_match_case_sensitively()
    {
        // Unlike the target kind, the verdict name is exact: "dismissed" is as unknown as garbage.
        using var factory = CreateFactory(new InMemoryReportRepository([]), new InMemoryVerdictRepository([]));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/moderation/cases/Finding/{ReportedFindingId}/verdict", new { verdict = "dismissed" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-verdict", problem!.Type);
    }

    private sealed record CaseResponse(
        string TargetKind,
        Guid TargetId,
        Guid FindingId,
        string Preview,
        string Author,
        int ReportCount,
        IReadOnlyList<CaseReportResponse> Reports);

    private sealed record CaseReportResponse(
        string PointCitation, string PointText, string? Note, DateTimeOffset FiledAt);

    private sealed record MyReportResponse(bool Reported);

    private sealed record ProblemResponse(string Type);
}
