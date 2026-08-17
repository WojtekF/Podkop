using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Podkop.Documents.Application;
using Podkop.Documents.Domain;
using Podkop.Documents.Infrastructure;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;
using Podkop.Moderation.Application;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Server.Tests;

/// <summary>
///     ADR 0008 end to end: a report is a moderation signal only, so filing one — against the
///     finding (issue #32) or against a comment (issue #33) — changes no score, vote, or
///     promotion state. The proof reads the finding and its discussion through the same public
///     surfaces the frontend uses, before and after filing — a cross-slice observation that
///     belongs to the composition root's tests, where the full wiring is under test (ADR 0003).
/// </summary>
public class ReportSideEffectsTests
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");
    private static readonly Guid ReplyId = Guid.Parse("0d4f9a3e-4444-4222-8333-444455556666");
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
                // The discussion under scrutiny: a voted-on top-level comment and a reply, both
                // authored by others, so the comment report action is live and the vote counts
                // the filing must not touch are non-trivial.
                services.AddSingleton<ICommentRepository>(provider => new InMemoryCommentRepository(
                [
                    new Comment(CommentId, FindingId, null, "grace_hopper",
                        "A comment under scrutiny.", At("2026-06-08T10:00:00Z"),
                        new Dictionary<string, VoteDirection> { ["linus_torvalds"] = VoteDirection.Up }),
                    new Comment(ReplyId, FindingId, CommentId, "linus_torvalds",
                        "A reply under scrutiny.", At("2026-06-08T11:00:00Z")),
                ], provider.GetRequiredService<IPublisher>()));
                // Reports seed by default since issue #34; this proof is about the act of
                // filing, so it starts from an empty slate rather than the sample reports.
                services.AddSingleton<IReportRepository>(new InMemoryReportRepository([]));
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

    [Fact]
    public async Task Filing_a_comment_report_changes_no_vote_count_or_discussion_state()
    {
        // The discussion and the finding are re-read raw through the same public surfaces the
        // frontend uses — byte-equal answers mean no count, vote, or promotion state moved.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var discussionBefore = await client.GetStringAsync($"/api/findings/{FindingId}/comments");
        var findingBefore = await client.GetStringAsync($"/api/findings/{FindingId}");

        var response = await client.PostAsJsonAsync($"/api/comments/{CommentId}/my-report",
            new { statutePointId = SpamPointId, note = "Spam in the discussion." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal(discussionBefore, await client.GetStringAsync($"/api/findings/{FindingId}/comments"));
        Assert.Equal(findingBefore, await client.GetStringAsync($"/api/findings/{FindingId}"));
    }

    private sealed record FindingDetailResponse(
        int DigCount,
        string? MyVote,
        int CommentCount,
        DateTimeOffset? PromotedAt);
}
