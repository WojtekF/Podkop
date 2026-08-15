using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Moderation.Domain;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Moderation.Tests;

/// <summary>
///     The shipped report seed (issue #34). The generator contract is asserted on
///     <see cref="SampleReports.GenerateFor" /> directly, against a synthetic world this test
///     owns; the last spec runs the app as shipped, no overrides, through the same HTTP surface
///     the frontend uses — the stub acting user is a seeded Moderator (issue #31), so the queue
///     answers them. Every spec fails until the seed generation is written (red-only scaffold).
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

    [Fact]
    public void Every_generated_report_could_have_been_filed()
    {
        var reports = SampleReports.GenerateFor(Targets, CitableVersions);

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

        // The filing rule: one report per reporter per target.
        Assert.Distinct(reports.Select(r => (r.Reporter, r.TargetKind, r.TargetId)));
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
}
