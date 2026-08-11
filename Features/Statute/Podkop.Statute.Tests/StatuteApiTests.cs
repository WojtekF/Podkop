using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Podkop.Statute.Application;
using Podkop.Statute.Domain;
using Podkop.Statute.Infrastructure;

namespace Podkop.Statute.Tests;

public class StatuteApiTests
{
    // Point ids are the stable identity a Report will cite (ADR 0006): the same point keeps the
    // same id in every seeded version below, even where its text changes.
    private static readonly Guid PurposePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");
    private static readonly Guid ConsequencesPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000004");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>
    ///     A full statute version: purpose framing (never reportable), conduct rules (the only
    ///     reportable points), consequences framing (never reportable). Texts carry the version
    ///     marker so retrieving the wrong version is visible in content, not just in the number.
    /// </summary>
    private static StatuteVersion Version(int version, DateTimeOffset effectiveFrom)
        => new(version, effectiveFrom,
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(PurposePointId, 1,
                    $"Podkop is a community for sharing and judging findings. (v{version})", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, $"Do not post spam. (v{version})", true),
                new StatutePoint(HatePointId, 2, $"Do not post hateful content. (v{version})", true),
            ]),
            new StatuteSection(3, "Consequences",
            [
                new StatutePoint(ConsequencesPointId, 1,
                    $"Moderators may remove content, redact it, or ban the author. (v{version})", false),
            ]),
        ]);

    // Which version is "in force" is a fact about an instant, so every spec pins the clock
    // (FakeTimeProvider) instead of inheriting whatever instant the test run happens at.
    private static WebApplicationFactory<Program> CreateFactory(DateTimeOffset now, params StatuteVersion[] versions)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
                services.AddSingleton<IStatuteRepository>(new InMemoryStatuteRepository(versions));
            }));

    [Fact]
    public async Task Current_statute_is_the_version_in_force_at_the_pinned_instant()
    {
        // Falsifiable on purpose: version 3 is published but not yet in force at the pinned
        // instant, so every plausible wrong rule — highest version, lowest version, first seeded,
        // last seeded, latest effective-from — picks a different version than the in-force v2.
        using var factory = CreateFactory(At("2026-07-01T00:00:00Z"),
            Version(3, At("2099-01-01T00:00:00Z")),
            Version(2, At("2026-06-01T00:00:00Z")),
            Version(1, At("2025-01-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/statute");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statute = await response.Content.ReadFromJsonAsync<StatuteResponse>();
        Assert.NotNull(statute);
        Assert.Equal(2, statute.Version);
        Assert.Equal(At("2026-06-01T00:00:00Z"), statute.EffectiveFrom);
    }

    [Fact]
    public async Task Current_statute_carries_sections_and_numbered_points_with_reportable_flags()
    {
        using var factory = CreateFactory(At("2026-07-01T00:00:00Z"), Version(1, At("2025-01-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var statute = await client.GetFromJsonAsync<StatuteResponse>("/api/statute");

        Assert.NotNull(statute);
        Assert.Equal([1, 2, 3], statute.Sections.Select(s => s.Number));
        Assert.Equal(["Purpose of the service", "Rules of conduct", "Consequences"],
            statute.Sections.Select(s => s.Title));

        var conduct = statute.Sections.Single(s => s.Number == 2);
        Assert.Equal([1, 2], conduct.Points.Select(p => p.Number));
        Assert.Equal([SpamPointId, HatePointId], conduct.Points.Select(p => p.Id));
        Assert.Equal("Do not post spam. (v1)", conduct.Points[0].Text);
        Assert.All(conduct.Points, p => Assert.True(p.IsReportable));

        // The framing sections can never be cited by a report — their points are not reportable.
        var framingPoints = statute.Sections.Where(s => s.Number != 2).SelectMany(s => s.Points).ToList();
        Assert.NotEmpty(framingPoints);
        Assert.All(framingPoints, p => Assert.False(p.IsReportable));
    }

    [Fact]
    public async Task Historical_version_stays_readable_by_number()
    {
        using var factory = CreateFactory(At("2026-07-01T00:00:00Z"),
            Version(1, At("2025-01-01T00:00:00Z")),
            Version(2, At("2026-06-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var statute = await client.GetFromJsonAsync<StatuteResponse>("/api/statute/versions/1");

        Assert.NotNull(statute);
        Assert.Equal(1, statute.Version);
        Assert.Equal(At("2025-01-01T00:00:00Z"), statute.EffectiveFrom);
        // The superseded content, not the current one.
        Assert.Contains("(v1)", statute.Sections.Single(s => s.Number == 2).Points[0].Text);
    }

    [Fact]
    public async Task Unknown_version_is_a_404()
    {
        using var factory = CreateFactory(At("2026-07-01T00:00:00Z"),
            Version(1, At("2025-01-01T00:00:00Z")),
            Version(2, At("2026-06-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/statute/versions/9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Current_statute_is_a_404_when_no_version_is_in_force_yet()
    {
        // The clock is pinned before v1's effective-from: nothing is in force at that instant,
        // however far in the past the version looks to the test run's real clock.
        using var factory = CreateFactory(At("2024-12-31T23:59:59Z"), Version(1, At("2025-01-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/statute");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Previous_version_stays_in_force_until_the_amendment_instant()
    {
        // One second before the amendment's effective-from, the old version still rules —
        // even though the amendment is long in force by the test run's real clock.
        using var factory = CreateFactory(At("2026-05-31T23:59:59Z"),
            Version(1, At("2025-01-01T00:00:00Z")),
            Version(2, At("2026-06-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var statute = await client.GetFromJsonAsync<StatuteResponse>("/api/statute");

        Assert.NotNull(statute);
        Assert.Equal(1, statute.Version);
        Assert.Equal(At("2025-01-01T00:00:00Z"), statute.EffectiveFrom);
    }

    [Fact]
    public async Task Amendment_takes_force_at_exactly_its_effective_instant()
    {
        // The boundary is inclusive: at the effective instant itself the amendment already
        // rules. The far-future date keeps the real clock on the wrong side of the boundary,
        // so only a handler consulting the injected clock can pass.
        using var factory = CreateFactory(At("2099-01-01T00:00:00Z"),
            Version(1, At("2025-01-01T00:00:00Z")),
            Version(2, At("2099-01-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var statute = await client.GetFromJsonAsync<StatuteResponse>("/api/statute");

        Assert.NotNull(statute);
        Assert.Equal(2, statute.Version);
    }

    private sealed record StatuteResponse(
        int Version,
        DateTimeOffset EffectiveFrom,
        List<SectionResponse> Sections);

    private sealed record SectionResponse(
        int Number,
        string Title,
        List<PointResponse> Points);

    private sealed record PointResponse(
        Guid Id,
        int Number,
        string Text,
        bool IsReportable);
}
