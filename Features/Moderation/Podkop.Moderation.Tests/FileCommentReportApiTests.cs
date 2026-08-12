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
///     Filing a comment report (issue #33) through the HTTP seam: POST my-report on a comment
///     files the stub user's one report on that comment — top-level or reply — under exactly the
///     finding-report rules (one per user per comment, no self-reports, pinned point and version
///     per ADR 0006). The unknown-target and own-content refusals name the comment kind in their
///     problem type; every other refusal reads the same as the finding endpoint's. The point
///     vocabulary (retired, future, unknown points) is pinned by the finding-report suite — the
///     rules are shared, so one representative not-reportable case suffices here. The wire-level
///     point-required rejection is likewise shared and specified there. The world outside the
///     slice enters only through its own ports (ADR 0003), stubbed here.
/// </summary>
public class FileCommentReportApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid TargetCommentId = Guid.Parse("c0000000-1111-4222-8333-444455556666");
    private static readonly Guid OwnCommentId = Guid.Parse("c0000000-2222-4222-8333-444455556666");

    private static readonly Guid PurposePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");

    /// <summary>The current Statute as the port answers it: v2, citing spam and hate as reportable.</summary>
    private static readonly CurrentStatute CurrentStatuteV2 = new(2, [SpamPointId, HatePointId]);

    /// <summary>The filing instant every spec pins.</summary>
    private static readonly DateTimeOffset Now = At("2026-07-01T12:00:00Z");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static WebApplicationFactory<Program> CreateFactory(InMemoryReportRepository reports) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                services.AddSingleton<IReportTargetLookup>(new StubReportTargetLookup(
                    (ReportTargetKind.Comment, new ReportTarget(TargetCommentId, "grace_hopper")),
                    (ReportTargetKind.Comment, new ReportTarget(OwnCommentId, StubUser))));
                services.AddSingleton<IStatuteLookup>(new StubStatuteLookup(CurrentStatuteV2));
                services.AddSingleton<IReportRepository>(reports);
            }));

    private static Task<HttpResponseMessage> Post(HttpClient client, Guid commentId, Guid? statutePointId,
        string? note = null) =>
        client.PostAsJsonAsync($"/api/comments/{commentId}/my-report", new { statutePointId, note });

    [Fact]
    public async Task Filing_a_comment_report_returns_201_with_the_reported_state()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetCommentId, SpamPointId, "Spam in the discussion.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/comments/{TargetCommentId}/my-report",
            response.Headers.Location?.ToString());
        var status = await response.Content.ReadFromJsonAsync<MyReportResponse>();
        Assert.NotNull(status);
        Assert.True(status.Reported);
    }

    [Fact]
    public async Task The_stored_report_pins_the_comment_the_point_the_current_version_and_the_instant()
    {
        var reports = new InMemoryReportRepository([]);
        using var factory = CreateFactory(reports);
        using var client = factory.CreateClient();

        var response = await Post(client, TargetCommentId, SpamPointId, "  Spam in the discussion. \n");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await reports.GetByReporterAndTargetAsync(StubUser, ReportTargetKind.Comment,
            TargetCommentId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(StubUser, stored.Reporter);
        Assert.Equal(ReportTargetKind.Comment, stored.TargetKind);
        Assert.Equal(TargetCommentId, stored.TargetId);
        Assert.Equal(SpamPointId, stored.StatutePointId);
        // The version the statute port answered at the pinned filing instant (ADR 0006).
        Assert.Equal(2, stored.StatuteVersion);
        Assert.Equal("Spam in the discussion.", stored.Note);
        Assert.Equal(Now, stored.FiledAt);
    }

    [Fact]
    public async Task A_second_report_of_the_same_comment_by_the_same_user_is_refused()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var first = await Post(client, TargetCommentId, SpamPointId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Citing a different point changes nothing — the rule is one report per user per
        // comment, not per point.
        var second = await Post(client, TargetCommentId, HatePointId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:already-reported", problem!.Type);
    }

    [Fact]
    public async Task A_finding_report_never_counts_as_a_comment_report_even_with_a_colliding_id()
    {
        // An earlier report whose target id equals the comment's id but whose kind is Finding:
        // the one-report-per-user-per-target rule is scoped by kind, so filing on the comment
        // must still succeed. A kind-blind duplicate lookup answers 409 here.
        var earlier = new Report(Guid.Parse("d0000000-0000-4000-8000-000000000001"), StubUser,
            ReportTargetKind.Finding, TargetCommentId, SpamPointId, statuteVersion: 1, note: null,
            At("2026-05-01T00:00:00Z"));
        using var factory = CreateFactory(new InMemoryReportRepository([earlier]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetCommentId, SpamPointId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Authors_cannot_report_their_own_comment()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, OwnCommentId, SpamPointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:own-comment", problem!.Type);
    }

    [Fact]
    public async Task A_point_that_is_not_reportable_cannot_be_cited()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetCommentId, PurposePointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:point-not-reportable", problem!.Type);
    }

    [Fact]
    public async Task A_note_over_the_cap_is_refused()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetCommentId, SpamPointId,
            new string('x', Report.MaxNoteLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:report-note-too-long", problem!.Type);
    }

    [Fact]
    public async Task Filing_against_an_unknown_comment_is_a_404()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, Guid.Parse("c0000000-9999-4222-8333-444455556666"), SpamPointId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-comment", problem!.Type);
    }

    private sealed record MyReportResponse(bool Reported);

    private sealed record ProblemResponse(string Type);
}
