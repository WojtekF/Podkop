using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Application;
using Podkop.Findings.Domain;
using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;
using Podkop.Users.Application;
using Podkop.Users.Domain;
using Podkop.Moderation.Infrastructure;

namespace Podkop.Server.Tests;

/// <summary>
///     ADR 0008 end to end for the verdict (issue #35): a dismissal is a moderation record
///     only, so issuing one — against a finding's case or a comment's — leaves the judged
///     content untouched: still served, same score, same votes, same comment count, same
///     promotion state. The proof reads the finding and its discussion through the same public
///     surfaces the frontend uses, before and after dismissing — a cross-slice observation
///     that belongs to the composition root's tests, where the full wiring is under test
///     (ADR 0003): the stub acting user is a seeded Moderator (issue #31) and the real
///     adapters answer the moderation ports.
/// </summary>
public class DismissSideEffectsTests
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");
    private static readonly Guid ReplyId = Guid.Parse("0d4f9a3e-4444-4222-8333-444455556666");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Report ReportBy(string reporter, ReportTargetKind kind, Guid targetId) =>
        new(Guid.CreateVersion7(), reporter, kind, targetId, SpamPointId, statuteVersion: 2,
            note: null, At("2026-06-20T10:00:00Z"));

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(At("2026-07-01T12:00:00Z")));
                // Authored by others than the stub moderator, so the dismissals are not
                // refused as own-case — the content facts flow through the real adapters.
                services.AddSingleton<IFindingRepository>(new StubFindingRepository(
                [
                    new Finding(
                        id: FindingId,
                        title: "A finding under scrutiny",
                        description: "The finding the case is about.",
                        source: new Uri("https://blog.example.org/posts/42"),
                        thumbnail: null,
                        author: "grace_hopper",
                        tags: ["angular"],
                        createdAt: At("2026-06-08T03:30:00Z"),
                        promotedAt: At("2026-06-08T09:30:00Z"),
                        commentCount: 0),
                ]));
                // The discussion under judgment: a voted-on top-level comment and a reply, so
                // the vote counts the dismissal must not touch are non-trivial.
                services.AddSingleton<ICommentRepository>(provider => new InMemoryCommentRepository(
                [
                    new Comment(CommentId, FindingId, null, "grace_hopper",
                        "A comment under scrutiny.", At("2026-06-08T10:00:00Z"),
                        new Dictionary<string, VoteDirection> { ["linus_torvalds"] = VoteDirection.Up }),
                    new Comment(ReplyId, FindingId, CommentId, "linus_torvalds",
                        "A reply under scrutiny.", At("2026-06-08T11:00:00Z")),
                ], provider.GetRequiredService<IPublisher>()));
                // One open case per target kind, pending until the spec dismisses it; the
                // verdict slate starts empty rather than with the sample dismissals.
                services.AddSingleton<IReportRepository>(new InMemoryReportRepository(
                [
                    ReportBy("margaret_h", ReportTargetKind.Finding, FindingId),
                    ReportBy("margaret_h", ReportTargetKind.Comment, CommentId),
                ]));
                services.AddSingleton<IVerdictRepository>(new InMemoryVerdictRepository([]));
                // The dismissing stub user must hold the Moderator role; user records live in
                // PostgreSQL since issue #89, so the store is doubled at the slice's port.
                services.AddSingleton<IUserRepository>(new StubUserRepository(
                    new User("ada_lovelace", UserRole.Moderator)));
            }));

    private static Task<HttpResponseMessage> Dismiss(HttpClient client, string targetKind, Guid targetId) =>
        client.PostAsJsonAsync($"/api/moderation/cases/{targetKind}/{targetId}/verdict",
            new { verdict = "Dismissed" });

    [Fact]
    public async Task Dismissing_a_findings_case_changes_no_score_vote_or_promotion_state()
    {
        // The finding is re-read through the same public surface the frontend uses, before
        // and after the dismissal — it must still be served, unchanged.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");

        var response = await Dismiss(client, "Finding", FindingId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await client.GetFromJsonAsync<FindingDetailResponse>($"/api/findings/{FindingId}");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Dismissing_a_comments_case_changes_no_vote_count_or_discussion_state()
    {
        // The discussion and the finding are re-read raw through the same public surfaces the
        // frontend uses — byte-equal answers mean no count, vote, or promotion state moved
        // and nothing disappeared.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var discussionBefore = await client.GetStringAsync($"/api/findings/{FindingId}/comments");
        var findingBefore = await client.GetStringAsync($"/api/findings/{FindingId}");

        var response = await Dismiss(client, "Comment", CommentId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(discussionBefore, await client.GetStringAsync($"/api/findings/{FindingId}/comments"));
        Assert.Equal(findingBefore, await client.GetStringAsync($"/api/findings/{FindingId}"));
    }

    private sealed record FindingDetailResponse(
        int DigCount,
        string? MyVote,
        int CommentCount,
        DateTimeOffset? PromotedAt);
}
