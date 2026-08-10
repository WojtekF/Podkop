using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.Statute.Application;
using Podkop.Statute.Domain;
using Podkop.Statute.Infrastructure;

namespace Podkop.Statute.Tests;

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

    private static WebApplicationFactory<Program> CreateFactory(params PrivacyPolicyVersion[] versions)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IPrivacyPolicyRepository>(new InMemoryPrivacyPolicyRepository(versions))));

    [Fact]
    public async Task Current_privacy_policy_is_the_version_in_force_today()
    {
        // Same falsifiable seeding as the statute's: the future-dated version 3 makes every
        // plausible wrong rule pick a different version than the in-force v2.
        using var factory = CreateFactory(
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
        using var factory = CreateFactory(Version(1, At("2025-02-01T00:00:00Z")));
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
        using var factory = CreateFactory(
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
        using var factory = CreateFactory(Version(1, At("2025-02-01T00:00:00Z")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/privacy-policy/versions/9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
