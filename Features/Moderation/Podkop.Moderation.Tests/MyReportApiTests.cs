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
///     The my-report read (issue #32) through the HTTP seam: GET my-report answers the one
///     member-visible fact about a finding's reports — whether the current (stub) user filed
///     one — so the detail page can show the already-reported state from its first render.
///     Other users' reports never show through it, and only a PENDING report counts
///     (issue #35): a report a Verdict resolved answers not-reported again. The world outside
///     the slice enters only through its own ports (ADR 0003), stubbed here.
/// </summary>
public class MyReportApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Report ReportBy(string reporter) =>
        new(Guid.Parse("d0000000-0000-4000-8000-000000000001"), reporter, ReportTargetKind.Finding,
            FindingId, SpamPointId, statuteVersion: 2, note: null, At("2026-07-01T12:00:00Z"));

    private static WebApplicationFactory<Program> CreateFactory(IReadOnlyList<Report> reports,
        InMemoryVerdictRepository? verdicts = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // No verdict has been issued unless the spec says so — every report pending.
                services.AddSingleton<IVerdictRepository>(verdicts ?? new InMemoryVerdictRepository([]));
                // The clock and a one-point current Statute are pinned so the filing spec below
                // can cite a known reportable point; the read specs never touch either.
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(At("2026-07-01T12:00:00Z")));
                services.AddSingleton<IStatuteLookup>(
                    new StubStatuteLookup(new CurrentStatute(2, [SpamPointId])));
                services.AddSingleton<IReportTargetLookup>(new StubReportTargetLookup(
                    (ReportTargetKind.Finding, new ReportTarget(FindingId, "grace_hopper"))));
                services.AddSingleton<IReportRepository>(new InMemoryReportRepository(reports));
            }));

    [Fact]
    public async Task My_report_is_false_when_the_user_has_not_reported()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/findings/{FindingId}/my-report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<MyReportResponse>();
        Assert.NotNull(status);
        Assert.False(status.Reported);
    }

    [Fact]
    public async Task My_report_is_true_for_a_report_filed_in_an_earlier_session()
    {
        using var factory = CreateFactory([ReportBy(StubUser)]);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyReportResponse>($"/api/findings/{FindingId}/my-report");

        Assert.NotNull(status);
        Assert.True(status.Reported);
    }

    [Fact]
    public async Task Another_users_report_does_not_show_as_mine()
    {
        using var factory = CreateFactory([ReportBy("grace_hopper")]);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyReportResponse>($"/api/findings/{FindingId}/my-report");

        Assert.NotNull(status);
        Assert.False(status.Reported);
    }

    [Fact]
    public async Task My_report_is_true_right_after_filing()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var filed = await client.PostAsJsonAsync($"/api/findings/{FindingId}/my-report",
            new { statutePointId = SpamPointId, note = (string?)null });
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);

        var status = await client.GetFromJsonAsync<MyReportResponse>($"/api/findings/{FindingId}/my-report");

        Assert.NotNull(status);
        Assert.True(status.Reported);
    }

    [Fact]
    public async Task A_resolved_report_no_longer_shows_as_mine()
    {
        // My-report is pending-scoped (issue #35): a dismissal resolved the stub user's
        // report, so the state resets to not-reported and the user may report afresh.
        var mine = ReportBy(StubUser);
        using var factory = CreateFactory([mine], new InMemoryVerdictRepository(
        [
            new Verdict(Guid.CreateVersion7(), "grace_hopper", ReportTargetKind.Finding, FindingId,
                VerdictKind.Dismissed, At("2026-07-02T12:00:00Z"), [mine.Id]),
        ]));
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<MyReportResponse>($"/api/findings/{FindingId}/my-report");

        Assert.NotNull(status);
        Assert.False(status.Reported);
    }

    [Fact]
    public async Task My_report_for_an_unknown_finding_is_a_404()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/findings/{Guid.Parse("0d4f9a3e-9999-4222-8333-444455556666")}/my-report");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record MyReportResponse(bool Reported);
}
