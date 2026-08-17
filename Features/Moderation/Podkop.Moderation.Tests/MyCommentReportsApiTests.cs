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
///     The batch my-reports read (issue #33) through the HTTP seam: GET comments/my-reports
///     answers, in one request, which comments of a finding's discussion the current (stub) user
///     already reported, so the detail page shows every comment's already-reported state from its
///     first render without one request per comment. Only the current user's reports show, only
///     comment-kind reports count, only comments of this finding's discussion are named — and
///     only PENDING reports (issue #35): a report a Verdict resolved drops out of the batch.
///     The world outside the slice enters only through its own ports (ADR 0003), stubbed here.
/// </summary>
public class MyCommentReportsApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    private static readonly Guid TopCommentId = Guid.Parse("c0000000-1111-4222-8333-444455556666");
    private static readonly Guid ReplyCommentId = Guid.Parse("c0000000-2222-4222-8333-444455556666");
    private static readonly Guid UnreportedCommentId = Guid.Parse("c0000000-3333-4222-8333-444455556666");

    /// <summary>A comment of some other finding's discussion — never in this finding's answer.</summary>
    private static readonly Guid ForeignCommentId = Guid.Parse("c0000000-4444-4222-8333-444455556666");

    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Report CommentReportBy(string reporter, Guid commentId) =>
        new(Guid.CreateVersion7(), reporter, ReportTargetKind.Comment, commentId, SpamPointId,
            statuteVersion: 2, note: null, At("2026-07-01T12:00:00Z"));

    private static WebApplicationFactory<Program> CreateFactory(IReadOnlyList<Report> reports,
        InMemoryVerdictRepository? verdicts = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // No verdict has been issued unless the spec says so — every report pending.
                services.AddSingleton<IVerdictRepository>(verdicts ?? new InMemoryVerdictRepository([]));
                // The discussion holds a top-level comment, a reply, and an unreported comment —
                // the batch answer must name reported ones of every depth and nothing else.
                services.AddSingleton<IFindingCommentsLookup>(new StubFindingCommentsLookup(
                    FindingId, TopCommentId, ReplyCommentId, UnreportedCommentId));
                services.AddSingleton<IReportRepository>(new InMemoryReportRepository(reports));
            }));

    private static Task<HttpResponseMessage> Get(HttpClient client, Guid findingId) =>
        client.GetAsync($"/api/findings/{findingId}/comments/my-reports");

    [Fact]
    public async Task Nothing_reported_answers_an_empty_list()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await Get(client, FindingId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<MyCommentReportsResponse>();
        Assert.NotNull(status);
        Assert.Empty(status.ReportedCommentIds);
    }

    [Fact]
    public async Task Exactly_the_comments_I_reported_are_named_replies_included()
    {
        using var factory = CreateFactory(
        [
            CommentReportBy(StubUser, TopCommentId),
            CommentReportBy(StubUser, ReplyCommentId),
        ]);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyCommentReportsResponse>(
            $"/api/findings/{FindingId}/comments/my-reports");

        Assert.NotNull(status);
        // No ordering is promised for the batch — compare as sets.
        Assert.Equal(2, status.ReportedCommentIds.Count);
        Assert.Contains(TopCommentId, status.ReportedCommentIds);
        Assert.Contains(ReplyCommentId, status.ReportedCommentIds);
    }

    [Fact]
    public async Task Another_users_report_does_not_show_as_mine()
    {
        using var factory = CreateFactory([CommentReportBy("grace_hopper", TopCommentId)]);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyCommentReportsResponse>(
            $"/api/findings/{FindingId}/comments/my-reports");

        Assert.NotNull(status);
        Assert.Empty(status.ReportedCommentIds);
    }

    [Fact]
    public async Task A_finding_report_never_shows_among_comment_reports()
    {
        // A finding-kind report whose target id happens to equal a comment's id: the batch is
        // comment-kind only, so a kind-blind query names it and fails here.
        var findingReport = new Report(Guid.CreateVersion7(), StubUser, ReportTargetKind.Finding,
            TopCommentId, SpamPointId, statuteVersion: 2, note: null, At("2026-07-01T12:00:00Z"));
        using var factory = CreateFactory([findingReport]);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyCommentReportsResponse>(
            $"/api/findings/{FindingId}/comments/my-reports");

        Assert.NotNull(status);
        Assert.Empty(status.ReportedCommentIds);
    }

    [Fact]
    public async Task My_report_on_another_findings_comment_does_not_show_here()
    {
        using var factory = CreateFactory([CommentReportBy(StubUser, ForeignCommentId)]);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyCommentReportsResponse>(
            $"/api/findings/{FindingId}/comments/my-reports");

        Assert.NotNull(status);
        Assert.Empty(status.ReportedCommentIds);
    }

    [Fact]
    public async Task A_resolved_comment_report_is_no_longer_named()
    {
        // The batch is pending-scoped too (issue #35): the top comment's report was resolved
        // by a dismissal, so only the reply's still-pending report is named.
        var resolved = CommentReportBy(StubUser, TopCommentId);
        var pending = CommentReportBy(StubUser, ReplyCommentId);
        using var factory = CreateFactory([resolved, pending], new InMemoryVerdictRepository(
        [
            new Verdict(Guid.CreateVersion7(), "grace_hopper", ReportTargetKind.Comment, TopCommentId,
                VerdictKind.Dismissed, At("2026-07-02T12:00:00Z"), [resolved.Id]),
        ]));
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyCommentReportsResponse>(
            $"/api/findings/{FindingId}/comments/my-reports");

        Assert.NotNull(status);
        Assert.Equal([ReplyCommentId], status.ReportedCommentIds);
    }

    [Fact]
    public async Task The_batch_for_an_unknown_finding_is_a_404()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await Get(client, Guid.Parse("0d4f9a3e-9999-4222-8333-444455556666"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record MyCommentReportsResponse(IReadOnlyList<Guid> ReportedCommentIds);
}
