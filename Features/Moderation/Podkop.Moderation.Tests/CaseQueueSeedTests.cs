using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Moderation.Domain;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Moderation.Tests;

/// <summary>
///     The shipped report and verdict seeds (issues #34/#35). The generator contracts are
///     asserted on <see cref="SampleReports.GenerateFor" /> and
///     <see cref="SampleVerdicts.GenerateFor" /> directly, against a synthetic world this test
///     owns; the as-shipped specs run the app with no overrides, through the same HTTP surface
///     the frontend uses — the stub acting user is a seeded Moderator (issue #31), so the
///     queue and the log answer them. The seeds tell one story: pending cases in the queue, at
///     least two dismissals in the log, and at least one target cleared and then re-reported,
///     so a fresh case sits next to its resolved history. The verdict-aware specs fail until
///     the verdict seed generation is written (red-only scaffold).
/// </summary>
public class CaseQueueSeedTests
{
    private static readonly Guid FirstFindingId = Guid.Parse("5eed0000-0000-4000-8000-000000000001");
    private static readonly Guid SecondFindingId = Guid.Parse("5eed0000-0000-4000-8000-000000000002");
    private static readonly Guid ThirdFindingId = Guid.Parse("5eed0000-0000-4000-8000-000000000003");
    private static readonly Guid FirstCommentId = Guid.Parse("5eed0000-0000-4000-8000-000000000011");
    private static readonly Guid SecondCommentId = Guid.Parse("5eed0000-0000-4000-8000-000000000012");

    private static readonly Guid SharedPointId = Guid.Parse("5eed0000-0000-4000-8000-000000000101");
    private static readonly Guid RetiredPointId = Guid.Parse("5eed0000-0000-4000-8000-000000000102");
    private static readonly Guid AddedPointId = Guid.Parse("5eed0000-0000-4000-8000-000000000103");

    private static readonly IReadOnlyList<SampleReportTarget> Targets =
    [
        new(ReportTargetKind.Finding, FirstFindingId, "grace_hopper"),
        new(ReportTargetKind.Finding, SecondFindingId, "margaret_h"),
        new(ReportTargetKind.Finding, ThirdFindingId, "ada_lovelace"),
        new(ReportTargetKind.Comment, FirstCommentId, "linus_t"),
        new(ReportTargetKind.Comment, SecondCommentId, "dennis_r"),
    ];

    /// <summary>v2 supersedes v1: one point survives, one is retired, one is added.</summary>
    private static readonly IReadOnlyList<SampleCitableVersion> CitableVersions =
    [
        new(1, [SharedPointId, RetiredPointId]),
        new(2, [SharedPointId, AddedPointId]),
    ];

    /// <summary>
    ///     Both seeded moderators author sample content (issue #31), so the verdict generator
    ///     always has a legal actor for any target — never the target's own author.
    /// </summary>
    private static readonly IReadOnlyList<string> Moderators = ["ada_lovelace", "grace_hopper"];

    [Fact]
    public void Every_generated_report_could_have_been_filed()
    {
        var reports = SampleReports.GenerateFor(Targets, CitableVersions);
        var verdicts = SampleVerdicts.GenerateFor(Targets, reports, Moderators);

        var authorPool = Targets.Select(target => target.Author).ToHashSet();
        Assert.All(reports, report =>
        {
            // The reported target is one of the given ones, and its own author never filed.
            var target = Assert.Single(Targets,
                t => t.Kind == report.TargetKind && t.Id == report.TargetId);
            Assert.NotEqual(target.Author, report.Reporter);
            Assert.Contains(report.Reporter, authorPool);

            // The citation is a reportable point of the version the report pins (ADR 0006).
            var pinned = Assert.Single(CitableVersions, v => v.Version == report.StatuteVersion);
            Assert.Contains(report.StatutePointId, pinned.ReportablePointIds);

            Assert.True(report.Note is null || report.Note.Length <= Report.MaxNoteLength);
        });

        // The filing rule is pending-scoped (issue #35): no reporter ever had two reports
        // pending on one target at once — reporters are unique within a target's resolved
        // history and within its fresh wave, though one reporter may span both.
        var resolvedIds = verdicts.SelectMany(v => v.ResolvedReportIds).ToHashSet();
        Assert.Distinct(reports.Where(r => resolvedIds.Contains(r.Id))
            .Select(r => (r.Reporter, r.TargetKind, r.TargetId)));
        Assert.Distinct(reports.Where(r => !resolvedIds.Contains(r.Id))
            .Select(r => (r.Reporter, r.TargetKind, r.TargetId)));
    }

    [Fact]
    public void Every_generated_verdict_could_have_been_issued()
    {
        var reports = SampleReports.GenerateFor(Targets, CitableVersions);
        var verdicts = SampleVerdicts.GenerateFor(Targets, reports, Moderators);

        // At least two dismissals, so the shipped log has an order to show.
        Assert.True(verdicts.Count >= 2);

        Assert.All(verdicts, verdict =>
        {
            // Only Dismissed ships with issue #35, issued by a seeded moderator — and never
            // on the moderator's own content.
            Assert.Equal(VerdictKind.Dismissed, verdict.Kind);
            Assert.Contains(verdict.Actor, Moderators);
            var target = Assert.Single(Targets,
                t => t.Kind == verdict.TargetKind && t.Id == verdict.TargetId);
            Assert.NotEqual(target.Author, verdict.Actor);

            // A dismissal resolves the whole pending case, never part of one: the resolved
            // ids are exactly the target's seeded reports filed before the verdict's instant
            // — so every id references a real seeded report, and none is left out.
            var expectedResolved = reports
                .Where(r => r.TargetKind == verdict.TargetKind && r.TargetId == verdict.TargetId
                            && r.FiledAt < verdict.IssuedAt)
                .Select(r => r.Id)
                .ToHashSet();
            Assert.NotEmpty(expectedResolved);
            Assert.True(expectedResolved.SetEquals(verdict.ResolvedReportIds),
                "A verdict must capture exactly its target's reports filed before it.");
        });

        // Distinct instants keep the newest-first log order stable and visible; one verdict
        // per target keeps the seeded history one-round.
        Assert.Distinct(verdicts.Select(v => v.IssuedAt));
        Assert.Distinct(verdicts.Select(v => (v.TargetKind, v.TargetId)));
    }

    [Fact]
    public void The_verdict_seed_tells_a_cleared_then_re_reported_story()
    {
        var reports = SampleReports.GenerateFor(Targets, CitableVersions);
        var verdicts = SampleVerdicts.GenerateFor(Targets, reports, Moderators);

        // At least one judged target was reported again after its dismissal: the shipped
        // queue carries a fresh case whose older sibling reports are resolved.
        Assert.Contains(verdicts, verdict => reports.Any(r =>
            r.TargetKind == verdict.TargetKind && r.TargetId == verdict.TargetId
            && r.FiledAt > verdict.IssuedAt));
    }

    [Fact]
    public void The_seed_covers_everything_the_queue_makes_observable()
    {
        var reports = SampleReports.GenerateFor(Targets, CitableVersions);

        Assert.Contains(reports, r => r.TargetKind == ReportTargetKind.Finding);
        Assert.Contains(reports, r => r.TargetKind == ReportTargetKind.Comment);

        // Grouping shows: some target carries several reports.
        Assert.Contains(reports.GroupBy(r => (r.TargetKind, r.TargetId)), g => g.Count() > 1);

        // The pinned-wording display shows: some report pins the superseded version.
        Assert.Contains(reports, r => r.StatuteVersion == 1);

        Assert.Contains(reports, r => r.Note is not null);
        Assert.Contains(reports, r => r.Note is null);

        // Distinct instants keep the oldest-grievance-first order stable and visible.
        Assert.Distinct(reports.Select(r => r.FiledAt));
    }

    [Fact]
    public async Task The_app_as_shipped_serves_a_queue_of_cases()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/moderation/cases");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cases = await response.Content.ReadFromJsonAsync<List<CaseResponse>>();
        Assert.NotNull(cases);
        Assert.NotEmpty(cases);
        Assert.Contains(cases, c => c.TargetKind == "Finding");
        Assert.Contains(cases, c => c.TargetKind == "Comment");
        Assert.Contains(cases, c => c.ReportCount > 1);
        Assert.All(cases, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Preview));
            Assert.True(c.Preview.Length <= Podkop.Moderation.Application.CaseSummary.MaxPreviewLength);
            Assert.Equal(c.ReportCount, c.Reports.Count);
            Assert.All(c.Reports, r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.PointCitation));
                Assert.False(string.IsNullOrWhiteSpace(r.PointText));
            });
        });
    }

    [Fact]
    public async Task The_app_as_shipped_serves_seeded_dismissals_from_the_log_newest_first()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/moderation/log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = await response.Content.ReadFromJsonAsync<List<LogEntryResponse>>();
        Assert.NotNull(entries);
        Assert.True(entries.Count >= 2);
        Assert.All(entries, entry =>
        {
            Assert.Equal("Dismissed", entry.Verdict);
            Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
            Assert.True(entry.ResolvedReportCount >= 1);
        });

        // Newest first, strictly — the seed's distinct instants leave no ties to hide behind.
        Assert.Equal(entries.Select(e => e.IssuedAt).OrderByDescending(at => at),
            entries.Select(e => e.IssuedAt));
        Assert.Distinct(entries.Select(e => e.IssuedAt));
    }

    [Fact]
    public async Task The_app_as_shipped_lists_a_cleared_target_again_with_only_its_fresh_reports()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var log = await client.GetFromJsonAsync<List<LogEntryResponse>>("/api/moderation/log");
        var cases = await client.GetFromJsonAsync<List<CaseResponse>>("/api/moderation/cases");
        Assert.NotNull(log);
        Assert.NotNull(cases);

        // At least one dismissed target is back in the queue — cleared, then re-reported —
        // and every such fresh case carries only reports filed after its last dismissal.
        var reReported = cases
            .Where(c => log.Any(e => e.TargetKind == c.TargetKind && e.TargetId == c.TargetId))
            .ToList();
        Assert.NotEmpty(reReported);
        Assert.All(reReported, freshCase =>
        {
            var lastDismissal = log
                .Where(e => e.TargetKind == freshCase.TargetKind && e.TargetId == freshCase.TargetId)
                .Max(e => e.IssuedAt);
            Assert.All(freshCase.Reports, report => Assert.True(report.FiledAt > lastDismissal));
        });
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

    private sealed record LogEntryResponse(
        string Actor,
        string TargetKind,
        Guid TargetId,
        string Verdict,
        DateTimeOffset IssuedAt,
        int ResolvedReportCount);
}
