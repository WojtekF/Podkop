using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Moderation.Tests;

/// <summary>
///     The moderator case queue through the HTTP seam (issue #34): GET /api/moderation/cases
///     lists every open Case — one per reported content, all its PENDING reports grouped under
///     it — oldest grievance first, previews cut to the cap, each report's citation resolved
///     against the version it pinned (ADR 0006), reporter identities withheld, and the whole
///     surface refused to anyone but a Moderator. Pending is derived (issue #35): a report a
///     Verdict resolved vanishes from its case, and a fully resolved target has no case at
///     all — the pending-scoping specs fail until GetCaseQueueHandler reads the verdicts. The
///     world outside the slice enters only through its own ports (ADR 0003), stubbed here; the
///     composition-root adapters behind them are specified in Podkop.Server.Tests.
/// </summary>
public class CaseQueueApiTests
{
    private const string StubUser = "ada_lovelace";

    // Target ids chosen so ascending-Guid order is legible in the specs that assert it.
    private static readonly Guid ReportedFindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid SecondFindingId = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");
    private static readonly Guid OwnFindingId = Guid.Parse("0d4f9a3e-5555-4222-8333-444455556666");
    private static readonly Guid ReportedCommentId = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");

    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");

    /// <summary>The current Statute as the port answers it — the queue never consults it, only pinned versions.</summary>
    private static readonly CurrentStatute CurrentStatuteV2 = new(2, [SpamPointId, HatePointId]);

    /// <summary>A comment long enough that only a cut preview can carry it.</summary>
    private static readonly string LongCommentText = new('c', CaseSummary.MaxPreviewLength + 60);

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Hosts the composition root with every collaborator the slice sees pinned through its
    ///     own ports: who moderates (the stub acting user, unless the spec demotes them), the
    ///     reported contents' facts, and the cited points as each pinned version worded them —
    ///     the spam point deliberately reads differently in v1 and v2.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(
        InMemoryReportRepository reports, bool actingUserIsModerator = true,
        InMemoryVerdictRepository? verdicts = null)
    {
        string[] moderators = actingUserIsModerator ? [StubUser] : [];
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // No verdict has been issued unless the spec says so — every report pending.
                services.AddSingleton<IVerdictRepository>(verdicts ?? new InMemoryVerdictRepository([]));
                services.AddSingleton<IModeratorLookup>(new StubModeratorLookup(moderators));
                services.AddSingleton<ICaseContentLookup>(new StubCaseContentLookup(
                    (ReportTargetKind.Finding, ReportedFindingId,
                        new CaseContent("grace_hopper", "A finding under scrutiny", ReportedFindingId)),
                    (ReportTargetKind.Finding, SecondFindingId,
                        new CaseContent("margaret_h", "Another finding under scrutiny", SecondFindingId)),
                    (ReportTargetKind.Finding, OwnFindingId,
                        new CaseContent(StubUser, "The moderator's own finding", OwnFindingId)),
                    (ReportTargetKind.Comment, ReportedCommentId,
                        new CaseContent("linus_t", LongCommentText, ReportedFindingId))));
                services.AddSingleton<IStatuteLookup>(new StubStatuteLookup(CurrentStatuteV2,
                    (SpamPointId, 1, new CitedPoint(2, 1, "Do not post spam. (v1)")),
                    (SpamPointId, 2, new CitedPoint(2, 1, "Do not post spam. (v2)")),
                    (HatePointId, 2, new CitedPoint(2, 3, "Do not post hateful content."))));
                services.AddSingleton<IReportRepository>(reports);
            }));
    }

    private static Report ReportBy(string reporter, ReportTargetKind kind, Guid targetId,
        Guid pointId, int version, string? note, string filedAtIso) =>
        new(Guid.NewGuid(), reporter, kind, targetId, pointId, version, note, At(filedAtIso));

    private static Task<HttpResponseMessage> GetQueue(HttpClient client) =>
        client.GetAsync("/api/moderation/cases");

    private static async Task<List<CaseResponse>> GetQueueCases(HttpClient client)
    {
        var response = await GetQueue(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cases = await response.Content.ReadFromJsonAsync<List<CaseResponse>>();
        Assert.NotNull(cases);
        return cases;
    }

    [Fact]
    public async Task The_queue_lists_one_case_per_reported_content_with_its_reports_grouped()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                "Links a spam farm.", "2026-07-02T10:00:00Z"),
            ReportBy("nick_chapsas", ReportTargetKind.Comment, ReportedCommentId, HatePointId, 2,
                null, "2026-07-02T11:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, HatePointId, 2,
                null, "2026-07-02T12:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        Assert.Equal(2, cases.Count);

        var findingCase = Assert.Single(cases, c => c.TargetId == ReportedFindingId);
        Assert.Equal("Finding", findingCase.TargetKind);
        Assert.Equal(ReportedFindingId, findingCase.FindingId);
        Assert.Equal("A finding under scrutiny", findingCase.Preview);
        Assert.Equal("grace_hopper", findingCase.Author);
        Assert.Equal(2, findingCase.ReportCount);
        Assert.Equal(2, findingCase.Reports.Count);

        var commentCase = Assert.Single(cases, c => c.TargetId == ReportedCommentId);
        Assert.Equal("Comment", commentCase.TargetKind);
        // A comment case's finding page is the finding the comment lives on, not the comment.
        Assert.Equal(ReportedFindingId, commentCase.FindingId);
        Assert.Equal("linus_t", commentCase.Author);
        Assert.Equal(1, commentCase.ReportCount);
        Assert.Single(commentCase.Reports);
    }

    [Fact]
    public async Task The_queue_orders_cases_oldest_grievance_first()
    {
        // Values chosen so every plausible wrong ordering answers a different sequence than
        // the specified earliest-report-first one. Expected: Second (09:00), Reported (10:00),
        // Comment (11:00). By report count: Second, Comment, Reported. By latest activity:
        // Second (13:30), Comment (11:30), Reported. Newest grievance first: Comment,
        // Reported, Second. By repository insertion: Reported, Comment, Second.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                null, "2026-07-02T10:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Comment, ReportedCommentId, HatePointId, 2,
                null, "2026-07-02T11:00:00Z"),
            ReportBy("milan_jovanovic", ReportTargetKind.Comment, ReportedCommentId, SpamPointId, 2,
                null, "2026-07-02T11:30:00Z"),
            ReportBy("nick_chapsas", ReportTargetKind.Finding, SecondFindingId, SpamPointId, 2,
                null, "2026-07-02T13:00:00Z"),
            ReportBy("grace_hopper", ReportTargetKind.Finding, SecondFindingId, HatePointId, 2,
                null, "2026-07-02T09:00:00Z"),
            ReportBy("matt_pocock", ReportTargetKind.Finding, SecondFindingId, SpamPointId, 2,
                null, "2026-07-02T13:30:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        Assert.Equal([SecondFindingId, ReportedFindingId, ReportedCommentId],
            cases.Select(c => c.TargetId));
    }

    [Fact]
    public async Task Cases_with_equally_old_grievances_order_by_target_id()
    {
        // Inserted larger-id-first so insertion order cannot masquerade as the tie-break.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, SecondFindingId, SpamPointId, 2,
                null, "2026-07-02T10:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, HatePointId, 2,
                null, "2026-07-02T10:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        Assert.Equal([ReportedFindingId, SecondFindingId], cases.Select(c => c.TargetId));
    }

    [Fact]
    public async Task A_cases_reports_read_oldest_first()
    {
        // Inserted newest-first, so insertion order and the specified order disagree.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                null, "2026-07-02T12:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, HatePointId, 2,
                null, "2026-07-02T11:00:00Z"),
            ReportBy("nick_chapsas", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                null, "2026-07-02T10:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        var reports = Assert.Single(cases).Reports;
        Assert.Equal(
            [At("2026-07-02T10:00:00Z"), At("2026-07-02T11:00:00Z"), At("2026-07-02T12:00:00Z")],
            reports.Select(r => r.FiledAt));
    }

    [Fact]
    public async Task Each_report_shows_the_point_as_its_pinned_version_worded_it()
    {
        // The same stable point cited twice across an amendment (ADR 0006): the earlier report
        // pinned v1, the later v2 — the queue shows each reporter's actual citation, so the
        // same "2.1" carries different wording per row.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 1,
                null, "2026-05-01T10:00:00Z"),
            ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                null, "2026-07-02T10:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        var reports = Assert.Single(cases).Reports;
        Assert.Equal(["2.1", "2.1"], reports.Select(r => r.PointCitation));
        Assert.Equal(["Do not post spam. (v1)", "Do not post spam. (v2)"],
            reports.Select(r => r.PointText));
    }

    [Fact]
    public async Task A_report_row_carries_its_note_and_filing_time_but_never_its_reporter()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                "Links a spam farm.", "2026-07-02T10:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var response = await GetQueue(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("margaret_h", raw);

        var cases = await response.Content.ReadFromJsonAsync<List<CaseResponse>>();
        var report = Assert.Single(Assert.Single(cases!).Reports);
        Assert.Equal("Links a spam farm.", report.Note);
        Assert.Equal(At("2026-07-02T10:00:00Z"), report.FiledAt);
    }

    [Fact]
    public async Task A_long_comment_preview_is_cut_to_the_cap()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Comment, ReportedCommentId, HatePointId, 2,
                null, "2026-07-02T10:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        var preview = Assert.Single(cases).Preview;
        Assert.Equal(CaseSummary.MaxPreviewLength, preview.Length);
        Assert.Equal(LongCommentText[..CaseSummary.MaxPreviewLength], preview);
    }

    [Fact]
    public async Task A_case_about_the_moderators_own_content_stays_listed()
    {
        // The never-on-their-own-content rule constrains judging (issue #35), not viewing:
        // every moderator sees the same queue, nothing silently disappears from it.
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("grace_hopper", ReportTargetKind.Finding, OwnFindingId, SpamPointId, 2,
                null, "2026-07-02T10:00:00Z"),
        ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        var ownCase = Assert.Single(cases);
        Assert.Equal(OwnFindingId, ownCase.TargetId);
        Assert.Equal(StubUser, ownCase.Author);
    }

    [Fact]
    public async Task A_member_is_refused_the_queue()
    {
        using var factory = CreateFactory(new InMemoryReportRepository(
        [
            ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
                null, "2026-07-02T10:00:00Z"),
        ]), actingUserIsModerator: false);
        using var client = factory.CreateClient();

        var response = await GetQueue(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:moderators-only", problem!.Type);
    }

    [Fact]
    public async Task An_empty_queue_answers_an_empty_list()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        Assert.Empty(cases);
    }

    [Fact]
    public async Task Resolved_reports_no_longer_show_in_their_case()
    {
        // A dismissal resolved the two older reports; a fresh report arrived afterwards
        // (issue #35). Only the fresh one is pending, so the case lists exactly it.
        var older = ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
            null, "2026-07-02T10:00:00Z");
        var alsoOlder = ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, HatePointId, 2,
            null, "2026-07-02T11:00:00Z");
        var fresh = ReportBy("nick_chapsas", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
            null, "2026-07-03T10:00:00Z");
        using var factory = CreateFactory(new InMemoryReportRepository([older, alsoOlder, fresh]),
            verdicts: new InMemoryVerdictRepository(
            [
                new Verdict(Guid.CreateVersion7(), "grace_hopper", ReportTargetKind.Finding,
                    ReportedFindingId, VerdictKind.Dismissed, At("2026-07-02T12:00:00Z"),
                    [older.Id, alsoOlder.Id]),
            ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        var freshCase = Assert.Single(cases);
        Assert.Equal(ReportedFindingId, freshCase.TargetId);
        Assert.Equal(1, freshCase.ReportCount);
        Assert.Equal(At("2026-07-03T10:00:00Z"), Assert.Single(freshCase.Reports).FiledAt);
    }

    [Fact]
    public async Task A_fully_resolved_target_has_no_case()
    {
        // Every report of the finding is resolved, so its case is gone — "already dismissed"
        // and "never reported" look the same (issue #35); the untouched comment case stays.
        var resolved = ReportBy("margaret_h", ReportTargetKind.Finding, ReportedFindingId, SpamPointId, 2,
            null, "2026-07-02T10:00:00Z");
        var alsoResolved = ReportBy("dennis_r", ReportTargetKind.Finding, ReportedFindingId, HatePointId, 2,
            null, "2026-07-02T11:00:00Z");
        var pendingElsewhere = ReportBy("nick_chapsas", ReportTargetKind.Comment, ReportedCommentId,
            HatePointId, 2, null, "2026-07-02T12:00:00Z");
        using var factory = CreateFactory(
            new InMemoryReportRepository([resolved, alsoResolved, pendingElsewhere]),
            verdicts: new InMemoryVerdictRepository(
            [
                new Verdict(Guid.CreateVersion7(), "grace_hopper", ReportTargetKind.Finding,
                    ReportedFindingId, VerdictKind.Dismissed, At("2026-07-02T12:00:00Z"),
                    [resolved.Id, alsoResolved.Id]),
            ]));
        using var client = factory.CreateClient();

        var cases = await GetQueueCases(client);

        Assert.Equal(ReportedCommentId, Assert.Single(cases).TargetId);
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

    private sealed record ProblemResponse(string Type);
}
