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

namespace Podkop.Server.Tests;

/// <summary>
///     ADR 0008 end to end: a report is a moderation signal only, so filing one changes no
///     score, vote, or promotion state. The proof reads the finding through the Findings
///     slice's public surface before and after filing — a cross-slice observation that belongs
///     to the composition root's tests, where the full wiring is under test (ADR 0003).
/// </summary>
public class ReportSideEffectsTests
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
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
                // Authored by someone other than the stub user, so its report action is live.
                services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(
                [
                    new Finding(
                        id: FindingId,
                        title: "A finding under scrutiny",
                        description: "The finding the report targets.",
                        source: new Uri("https://blog.example.org/posts/42"),
                        thumbnail: null,
                        author: "grace_hopper",
                        tags: ["angular"],
                        createdAt: At("2026-06-08T03:30:00Z"),
                        promotedAt: At("2026-06-08T09:30:00Z"),
                        commentCount: 0),
                ]));
            }));

    [Fact]
    public async Task Filing_changes_no_score_vote_or_promotion_state()
    {
        // The finding is re-read through the same public surface the frontend uses, before and
        // after filing.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        var response = await client.PostAsJsonAsync($"/api/findings/{FindingId}/my-report",
            new { statutePointId = SpamPointId, note = "Links a spam farm." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");
        Assert.Equal(before, after);
    }

    private sealed record FindingDetailResponse(
        int DigCount,
        string? MyVote,
        int CommentCount,
        DateTimeOffset? PromotedAt);
}
