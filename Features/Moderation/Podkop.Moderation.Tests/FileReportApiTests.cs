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
///     Filing a report (issue #32) through the HTTP seam: POST my-report files the stub user's
///     one report on a finding, citing a reportable point of the current Statute and pinning its
///     version (ADR 0006). Duplicates — pending-scoped since issue #35: a resolved report
///     blocks nothing — and self-reports are refused, and every error answer
///     carries a stable <c>podkop:problem:&lt;slug&gt;</c> ProblemDetails type so same-status
///     outcomes stay distinguishable. The world outside the slice enters only through its own
///     ports (ADR 0003), stubbed here; the composition-root adapters behind them — and the
///     ADR 0008 proof that filing touches no score, which reads the finding through another
///     slice's public surface — are specified in Podkop.Server.Tests.
/// </summary>
public class FileReportApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid TargetFindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid OwnFindingId = Guid.Parse("0d4f9a3e-2222-4222-8333-444455556666");

    // Point ids are the stable identity a report cites (ADR 0006). The statute port answers
    // only the reportable point ids of the version in force, so every wrong way to cite a
    // point looks the same to this slice — absent from that list. The ids below document the
    // distinct upstream reasons a point is absent; which version is in force, and why, is the
    // Documents slice's concern, specified with its adapter in Podkop.Server.Tests.
    private static readonly Guid PurposePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");
    private static readonly Guid RetiredPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000004");
    private static readonly Guid FuturePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000005");
    private static readonly Guid UnknownPointId = Guid.Parse("aaaa0000-0000-4000-8000-00000000ffff");

    /// <summary>The current Statute as the port answers it: v2, citing spam and hate as reportable.</summary>
    private static readonly CurrentStatute CurrentStatuteV2 = new(2, [SpamPointId, HatePointId]);

    /// <summary>The filing instant every spec pins.</summary>
    private static readonly DateTimeOffset Now = At("2026-07-01T12:00:00Z");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Hosts the composition root with every collaborator the slice sees pinned through its
    ///     own ports: the clock, both report targets (one authored by the stub user), the current
    ///     Statute, and a report repository the spec keeps a reference to — reports are invisible
    ///     over HTTP by design, so the stored report's facts are asserted through the slice's own
    ///     repository port.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(
        InMemoryReportRepository reports, bool statuteInForce = true,
        InMemoryVerdictRepository? verdicts = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // No verdict has been issued unless the spec says so — every report pending.
                services.AddSingleton<IVerdictRepository>(verdicts ?? new InMemoryVerdictRepository([]));
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                services.AddSingleton<IReportTargetLookup>(new StubReportTargetLookup(
                    (ReportTargetKind.Finding, new ReportTarget(TargetFindingId, "grace_hopper")),
                    (ReportTargetKind.Finding, new ReportTarget(OwnFindingId, StubUser))));
                services.AddSingleton<IStatuteLookup>(
                    new StubStatuteLookup(statuteInForce ? CurrentStatuteV2 : null));
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

        var stored = Assert.Single(await reports.GetByReporterAndTargetAsync(StubUser, ReportTargetKind.Finding,
            TargetFindingId, CancellationToken.None));
        Assert.Equal(StubUser, stored.Reporter);
        Assert.Equal(ReportTargetKind.Finding, stored.TargetKind);
        Assert.Equal(TargetFindingId, stored.TargetId);
        Assert.Equal(SpamPointId, stored.StatutePointId);
        // The version the statute port answered at the pinned filing instant (ADR 0006).
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

        var stored = Assert.Single(await reports.GetByReporterAndTargetAsync(StubUser, ReportTargetKind.Finding,
            TargetFindingId, CancellationToken.None));
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
        // The earlier report is still pending — no verdict resolved it — so it still blocks.
        var earlier = new Report(Guid.Parse("d0000000-0000-4000-8000-000000000001"), StubUser,
            ReportTargetKind.Finding, TargetFindingId, SpamPointId, statuteVersion: 1, note: null,
            At("2026-05-01T00:00:00Z"));
        using var factory = CreateFactory(new InMemoryReportRepository([earlier]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, HatePointId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("podkop:problem:already-reported", problem!.Type);
    }

    [Fact]
    public async Task A_resolved_report_no_longer_blocks_a_fresh_one()
    {
        // The one-report-per-user-per-target rule is pending-scoped (issue #35): a dismissal
        // resolved the stub user's earlier report, so the same user reports the target afresh.
        var earlier = new Report(Guid.Parse("d0000000-0000-4000-8000-000000000001"), StubUser,
            ReportTargetKind.Finding, TargetFindingId, SpamPointId, statuteVersion: 1, note: null,
            At("2026-05-01T00:00:00Z"));
        using var factory = CreateFactory(new InMemoryReportRepository([earlier]),
            verdicts: new InMemoryVerdictRepository(
            [
                new Verdict(Guid.CreateVersion7(), "grace_hopper", ReportTargetKind.Finding,
                    TargetFindingId, VerdictKind.Dismissed, At("2026-06-01T00:00:00Z"), [earlier.Id]),
            ]));
        using var client = factory.CreateClient();

        var response = await Post(client, TargetFindingId, SpamPointId, "It is at it again.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_fresh_report_after_a_dismissal_blocks_a_duplicate_like_any_pending_one()
    {
        // Continues A_resolved_report_no_longer_blocks_a_fresh_one: once the user re-reports
        // cleared content, the fresh report is the pending one, and the one-pending-report rule
        // guards it — the resolved history on the same target blocks nothing, but it must not
        // hide the pending report either.
        var earlier = new Report(Guid.Parse("d0000000-0000-4000-8000-000000000001"), StubUser,
            ReportTargetKind.Finding, TargetFindingId, SpamPointId, statuteVersion: 1, note: null,
            At("2026-05-01T00:00:00Z"));
        using var factory = CreateFactory(new InMemoryReportRepository([earlier]),
            verdicts: new InMemoryVerdictRepository(
            [
                new Verdict(Guid.CreateVersion7(), "grace_hopper", ReportTargetKind.Finding,
                    TargetFindingId, VerdictKind.Dismissed, At("2026-06-01T00:00:00Z"), [earlier.Id]),
            ]));
        using var client = factory.CreateClient();

        var refiled = await Post(client, TargetFindingId, SpamPointId, "It is at it again.");
        Assert.Equal(HttpStatusCode.Created, refiled.StatusCode);

        var third = await Post(client, TargetFindingId, HatePointId);

        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        var problem = await third.Content.ReadFromJsonAsync<ProblemResponse>();
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
        // The purpose point exists in the current version — it is just never reportable, so
        // the port never lists it.
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
        // Reportable in the superseded v1 only, so absent from the current version's list:
        // reportability is a fact about the version in force at filing time, not about any
        // version that ever existed.
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
        // The port answers null — no version in force yet — so even a point that will be
        // reportable later cannot be cited.
        using var factory = CreateFactory(new InMemoryReportRepository([]), statuteInForce: false);
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

    private sealed record MyReportResponse(bool Reported);

    private sealed record ProblemResponse(string Type);
}
