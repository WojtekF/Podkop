using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Podkop.Statute.Tests;

/// <summary>
///     Runs the app as shipped — the real seed generators, no repository overrides — through
///     the same HTTP surface the frontend uses. Only the clock is pinned: which shipped version
///     is "in force" is judged at a fixed instant after the seeded amendment's effective-from,
///     so these assertions never drift as real time passes or future-dated versions ship.
/// </summary>
public class StatuteSeedCoherenceTests
{
    private static WebApplicationFactory<Program> CreateFactory()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<TimeProvider>(
                    new FakeTimeProvider(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)))));

    [Fact]
    public async Task Seeded_statute_is_in_force_with_reportable_conduct_rules_and_nonreportable_framing()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/statute");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statute = await response.Content.ReadFromJsonAsync<StatuteResponse>();
        Assert.NotNull(statute);
        // At least two versions ship (issue #30), so the one in force is an amendment.
        Assert.True(statute.Version >= 2);

        var points = statute.Sections.SelectMany(s => s.Points).ToList();
        // The report flow (issue #32) needs conduct rules to offer; the purpose and
        // consequences framing must not be offerable.
        Assert.Contains(points, p => p.IsReportable);
        Assert.Contains(points, p => !p.IsReportable);
    }

    [Fact]
    public async Task Seeded_statute_keeps_version_1_readable()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var statute = await client.GetFromJsonAsync<StatuteResponse>("/api/statute/versions/1");

        Assert.NotNull(statute);
        Assert.Equal(1, statute.Version);
    }

    [Fact]
    public async Task Seeded_privacy_policy_is_in_force_with_readable_sections()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/privacy-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policy = await response.Content.ReadFromJsonAsync<PolicyResponse>();
        Assert.NotNull(policy);
        Assert.NotEmpty(policy.Sections);
        Assert.All(policy.Sections, s => Assert.NotEmpty(s.Paragraphs));
    }

    private sealed record StatuteResponse(int Version, List<SectionResponse> Sections);

    private sealed record SectionResponse(int Number, string Title, List<PointResponse> Points);

    private sealed record PointResponse(Guid Id, int Number, string Text, bool IsReportable);

    private sealed record PolicyResponse(int Version, List<PolicySectionResponse> Sections);

    private sealed record PolicySectionResponse(int Number, string Title, List<string> Paragraphs);
}
