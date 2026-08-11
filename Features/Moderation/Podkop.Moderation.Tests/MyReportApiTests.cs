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
///     The my-report read (issue #32) through the HTTP seam: GET my-report answers the one
///     member-visible fact about a finding's reports — whether the current (stub) user filed
///     one — so the detail page can show the already-reported state from its first render.
///     Other users' reports never show through it.
/// </summary>
public class MyReportApiTests
{
    private const string StubUser = "ada_lovelace";
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(Guid id) =>
        new(
            id: id,
            title: "A finding under scrutiny",
            description: "The finding the report targets.",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: ["angular"],
            createdAt: At("2026-06-08T03:30:00Z"),
            promotedAt: null,
            commentCount: 0);

    private static Report ReportBy(string reporter) =>
        new(Guid.Parse("d0000000-0000-4000-8000-000000000001"), reporter, FindingId, SpamPointId,
            statuteVersion: 2, note: null, At("2026-07-01T12:00:00Z"));

    private static WebApplicationFactory<Program> CreateFactory(IReadOnlyList<Report> reports) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // The clock and a one-version Statute are pinned so the filing spec below can
                // cite a known reportable point; the read specs never touch either.
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(At("2026-07-01T12:00:00Z")));
                services.AddSingleton<IStatuteRepository>(new InMemoryStatuteRepository(
                [
                    new StatuteVersion(2, At("2026-06-01T00:00:00Z"),
                    [
                        new StatuteSection(2, "Rules of conduct",
                        [
                            new StatutePoint(SpamPointId, 1, "Do not post spam. (v2)", true),
                        ]),
                    ]),
                ]));
                services.AddSingleton<IFindingRepository>(
                    new InMemoryFindingRepository([CreateFinding(FindingId)]));
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
