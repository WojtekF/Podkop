using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Podkop.Documents.Application;
using Podkop.Documents.Domain;
using Podkop.Documents.Infrastructure;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Moderation.Tests;

/// <summary>
///     Filing a report (issue #32) through the HTTP seam: POST my-report files the stub user's
///     one report on a finding, citing a reportable point of the Statute version in force at the
///     pinned filing instant (ADR 0006). Duplicates and self-reports are refused, filing never
///     touches a score or promotion (ADR 0008), and every error answer carries a stable
///     <c>podkop:problem:&lt;slug&gt;</c> ProblemDetails type so same-status outcomes stay
///     distinguishable.
/// </summary>
public class FileReportApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid TargetFindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid OwnFindingId = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");

    // Point ids are the stable identity a report cites (ADR 0006). The seeded versions below
    // are arranged so every wrong validation source picks a different answer: the purpose
    // point exists in the current version but is never reportable, the retired point is
    // reportable only in the superseded v1, the future point only in the not-yet-in-force v3.
    private static readonly Guid PurposePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");
    private static readonly Guid RetiredPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000004");
    private static readonly Guid FuturePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000005");
    private static readonly Guid UnknownPointId = Guid.Parse("aaaa0000-0000-4000-8000-00000000ffff");

    /// <summary>The filing instant every spec pins: v2 is in force (v1 superseded, v3 not yet).</summary>
    private static readonly DateTimeOffset Now =
        At("2026-07-01T12:00:00Z");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static StatuteVersion[] SeededVersions() =>
    [
        new(1, At("2025-01-01T00:00:00Z"),
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(PurposePointId, 1, "Podkop is a community for sharing findings. (v1)", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, "Do not post spam. (v1)", true),
                new StatutePoint(RetiredPointId, 2, "Do not post off-topic content. (v1)", true),
            ]),
        ]),
        new(2, At("2026-06-01T00:00:00Z"),
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(PurposePointId, 1, "Podkop is a community for sharing findings. (v2)", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, "Do not post spam. (v2)", true),
                new StatutePoint(HatePointId, 2, "Do not post hateful content. (v2)", true),
            ]),
        ]),
        new(3, At("2099-01-01T00:00:00Z"),
        [
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, "Do not post spam. (v3)", true),
                new StatutePoint(HatePointId, 2, "Do not post hateful content. (v3)", true),
                new StatutePoint(FuturePointId, 3, "Do not impersonate other users. (v3)", true),
            ]),
        ]),
    ];

    private static Finding CreateFinding(Guid id, string author) =>
        new(
            id: id,
            title: "A finding under scrutiny",
            description: "The finding the report targets.",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: author,
            tags: ["angular"],
            createdAt: At("2026-06-08T03:30:00Z"),
            promotedAt: At("2026-06-08T09:30:00Z"),
            commentCount: 0);

    /// <summary>
    ///     Hosts the full composition root with every collaborator pinned: the clock, both
    ///     findings (one authored by the stub user), the three Statute versions, and a report
    ///     repository the spec keeps a reference to — reports are invisible over HTTP by design,
    ///     so the stored report's facts are asserted through the slice's own repository port.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(
        InMemoryReportRepository reports, DateTimeOffset? now = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(now ?? Now));
                services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(
                [
                    CreateFinding(TargetFindingId, "grace_hopper"),
                    CreateFinding(OwnFindingId, StubUser),
                ]));
                services.AddSingleton<IStatuteRepository>(new InMemoryStatuteRepository(SeededVersions()));
                services.AddSingleton<IReportRepository>(reports);
            }));

    private static Task<HttpResponseMessage> Post(HttpClient client, Guid findingId, Guid? statutePointId,
        string? note = null) =>
        client.PostAsJsonAsync($"/api/findings/{findingId}/my-report", new { statutePointId, note });

    [Fact]
    public async Task Filing_a_report_returns_201_with_the_reported_state()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId, "Links a spam farm.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/findings/{TargetFindingId}/my-report",
            response.Headers.Location?.ToString());
        var status = await response.Content.ReadFromJsonAsync<MyReportResponse>();
        Assert.NotNull(status);
        Assert.True(status.Reported);
    }

    [Fact]
    public async Task The_stored_report_pins_the_point_the_current_version_and_the_filing_instant()
    {
        var reports = new InMemoryReportRepository([]);
        using var factory = CreateFactory(reports);
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId, "  Links a spam farm. \n");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await reports.GetByReporterAndFindingAsync(StubUser, TargetFindingId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(StubUser, stored.Reporter);
        Assert.Equal(TargetFindingId, stored.FindingId);
        Assert.Equal(SpamPointId, stored.StatutePointId);
        // The version in force at the pinned instant — not the oldest (1), not the highest (3).
        Assert.Equal(2, stored.StatuteVersion);
        Assert.Equal("Links a spam farm.", stored.Note);
        Assert.Equal(Now, stored.FiledAt);
    }

    [Fact]
    public async Task A_report_without_a_note_stores_no_note()
    {
        var reports = new InMemoryReportRepository([]);
        using var factory = CreateFactory(reports);
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId, note: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await reports.GetByReporterAndFindingAsync(StubUser, TargetFindingId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.Note);
    }

    [Fact]
    public async Task A_second_report_of_the_same_finding_by_the_same_user_is_refused()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var first = await Post(client, TargetFindingId, SpamPointId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Citing a different point changes nothing — the rule is one report per user per
        // finding, not per point.
        var second = await Post(client, TargetFindingId, HatePointId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:already-reported", problem!.Type);
    }

    [Fact]
    public async Task A_report_filed_in_an_earlier_session_also_refuses_a_duplicate()
    {
        var earlier = new Report(Guid.Parse("d0000000-0000-4000-8000-000000000001"), StubUser,
            TargetFindingId, SpamPointId, statuteVersion: 1, note: null, At("2026-05-01T00:00:00Z"));
        using var factory = CreateFactory(new InMemoryReportRepository([earlier]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, HatePointId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:already-reported", problem!.Type);
    }

    [Fact]
    public async Task Authors_cannot_report_their_own_finding()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, OwnFindingId, SpamPointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:own-finding", problem!.Type);
    }

    [Fact]
    public async Task A_point_that_is_not_reportable_cannot_be_cited()
    {
        // The purpose point exists in the current version — it is just never reportable.
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, PurposePointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:point-not-reportable", problem!.Type);
    }

    [Fact]
    public async Task A_point_dropped_by_the_current_version_cannot_be_cited()
    {
        // Reportable in the superseded v1 only: reportability is a fact about the version in
        // force at filing time, not about any version that ever existed.
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, RetiredPointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:point-not-reportable", problem!.Type);
    }

    [Fact]
    public async Task A_point_only_reportable_in_a_future_version_cannot_be_cited_yet()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, FuturePointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:point-not-reportable", problem!.Type);
    }

    [Fact]
    public async Task An_unknown_point_cannot_be_cited()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, UnknownPointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:point-not-reportable", problem!.Type);
    }

    [Fact]
    public async Task When_no_statute_version_is_in_force_nothing_can_be_cited()
    {
        // The clock is pinned before v1's effective-from: no version is in force, so even a
        // point that will be reportable later cannot be cited.
        using var factory = CreateFactory(new InMemoryReportRepository([]), At("2024-12-31T23:59:59Z"));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:point-not-reportable", problem!.Type);
    }

    [Fact]
    public async Task A_report_must_cite_a_point()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, statutePointId: null, note: "No point cited.");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:report-point-required", problem!.Type);
    }

    [Fact]
    public async Task A_note_of_exactly_the_cap_is_accepted_over_the_wire()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId, new string('x', Report.MaxNoteLength));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_note_over_the_cap_is_refused()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId,
            new string('x', Report.MaxNoteLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:report-note-too-long", problem!.Type);
    }

    [Fact]
    public async Task Filing_against_an_unknown_finding_is_a_404()
    {
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var response = await Post(client, Guid.Parse("0d4f9a3e-9999-4222-8333-444455556666"), SpamPointId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:unknown-finding", problem!.Type);
    }

    [Fact]
    public async Task Filing_changes_no_score_vote_or_promotion_state()
    {
        // ADR 0008: a report is a moderation signal only. The finding is re-read through the
        // same public surface the frontend uses, before and after filing.
        using var factory = CreateFactory(new InMemoryReportRepository([]));
        using var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{TargetFindingId}");

        var response = await Post(client, TargetFindingId, SpamPointId, "Links a spam farm.");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{TargetFindingId}");
        Assert.Equal(before, after);
    }

    private sealed record MyReportResponse(bool Reported);

    private sealed record ProblemResponse(string Type);

    private sealed record FindingDetailResponse(
        int DigCount,
        string? MyVote,
        int CommentCount,
        DateTimeOffset? PromotedAt);
}
