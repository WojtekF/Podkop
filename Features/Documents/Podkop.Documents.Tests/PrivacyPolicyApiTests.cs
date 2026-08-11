using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Podkop.Documents.Application;
using Podkop.Documents.Domain;
using Podkop.Documents.Infrastructure;

namespace Podkop.Documents.Tests;

public class PrivacyPolicyApiTests
{
    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static PrivacyPolicyVersion Version(int version, DateTimeOffset effectiveFrom)
        => new(version, effectiveFrom,
        [
            new PolicySection(1, "Data we process",
            [
                $"We store the findings, comments, and votes you submit. (v{version})",
                $"We do not track you across other sites. (v{version})",
            ]),
            new PolicySection(2, "Your rights",
            [
                $"You may request the erasure of your account. (v{version})",
            ]),
        ]);

    // Same discipline as the statute suite: "in force" is a fact about an instant, so every
    // spec pins the clock (FakeTimeProvider) rather than inheriting the test run's.
    private static WebApplicationFactory<Program> CreateFactory(DateTimeOffset now, params PrivacyPolicyVersion[] versions)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
                services.AddSingleton<IPrivacyPolicyRepository>(new InMemoryPrivacyPolicyRepository(versions));
            }));

    [Fact]
    public async Task Current_privacy_policy_is_the_version_in_force_at_the_pinned_instant()
    {
        // Same falsifiable seeding as the statute's: the future-dated version 3 makes every
        // plausible wrong rule pick a different version than the in-force v2.
        using var factory = CreateFactory(At("2026-06-01T00:00:00Z"),
            Version(3, At("2099-01-01T00:00:00Z")),
            Version(2, At("2026-05-01T00:00:00Z")),
            Version(1, At("2025-02-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/privacy-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policy = await response.Content.ReadFromJsonAsync<PolicyResponse>();
        Assert.NotNull(policy);
        Assert.Equal(2, policy.Version);
        Assert.Equal(At("2026-05-01T00:00:00Z"), policy.EffectiveFrom);
    }

    [Fact]
    public async Task Current_privacy_policy_carries_sections_and_paragraphs()
    {
        using var factory = CreateFactory(At("2026-06-01T00:00:00Z"), Version(1, At("2025-02-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var policy = await client.GetFromJsonAsync<PolicyResponse>("/api/privacy-policy");

        Assert.NotNull(policy);
        Assert.Equal([1, 2], policy.Sections.Select(s => s.Number));
        Assert.Equal(["Data we process", "Your rights"], policy.Sections.Select(s => s.Title));
        Assert.Equal(
            [
                "We store the findings, comments, and votes you submit. (v1)",
                "We do not track you across other sites. (v1)",
            ],
            policy.Sections[0].Paragraphs);
        Assert.Equal(["You may request the erasure of your account. (v1)"], policy.Sections[1].Paragraphs);
    }

    [Fact]
    public async Task Historical_version_stays_readable_by_number()
    {
        using var factory = CreateFactory(At("2026-06-01T00:00:00Z"),
            Version(1, At("2025-02-01T00:00:00Z")),
            Version(2, At("2026-05-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var policy = await client.GetFromJsonAsync<PolicyResponse>("/api/privacy-policy/versions/1");

        Assert.NotNull(policy);
        Assert.Equal(1, policy.Version);
        Assert.Contains("(v1)", policy.Sections[0].Paragraphs[0]);
    }

    [Fact]
    public async Task Unknown_version_is_a_404()
    {
        using var factory = CreateFactory(At("2026-06-01T00:00:00Z"), Version(1, At("2025-02-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/privacy-policy/versions/9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Future_version_is_not_readable_by_number_before_its_effective_instant()
    {
        // The statute suite carries the full boundary matrix; this spec holds the policy's
        // by-number route to the same gate: a published-but-future version 404s until its
        // effective-from instant.
        using var factory = CreateFactory(At("2026-06-01T00:00:00Z"),
            Version(1, At("2025-02-01T00:00:00Z")),
            Version(2, At("2099-01-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/privacy-policy/versions/2");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Previous_version_stays_in_force_until_the_amendment_instant()
    {
        // The statute suite carries the full boundary matrix; this one spec holds the policy
        // handler to the same rule: one second before the amendment's effective-from, the old
        // version still rules, however long the amendment has been in force by the real clock.
        using var factory = CreateFactory(At("2026-04-30T23:59:59Z"),
            Version(1, At("2025-02-01T00:00:00Z")),
            Version(2, At("2026-05-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var policy = await client.GetFromJsonAsync<PolicyResponse>("/api/privacy-policy");

        Assert.NotNull(policy);
        Assert.Equal(1, policy.Version);
        Assert.Equal(At("2025-02-01T00:00:00Z"), policy.EffectiveFrom);
    }

    private sealed record PolicyResponse(
        int Version,
        DateTimeOffset EffectiveFrom,
        List<PolicySectionResponse> Sections);

    private sealed record PolicySectionResponse(
        int Number,
        string Title,
        List<string> Paragraphs);
}
