using System.Globalization;
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
using Podkop.Moderation.Domain;

namespace Podkop.Server.Tests;

/// <summary>
///     The composition-root adapters behind the Moderation slice's ports (issues #32/#33).
///     Slices never reference each other's internals (ADR 0003), so the host is the one place
///     where "what Documents, Findings, and FindingComments hold" is mapped into "what
///     Moderation may see" — and the one place that mapping can be specified. The adapters are
///     resolved through the app's own DI wiring, never constructed by hand.
/// </summary>
public class ModerationPortAdapterTests
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("0d4f9a3e-3333-4222-8333-444455556666");
    private static readonly Guid ReplyId = Guid.Parse("0d4f9a3e-4444-4222-8333-444455556666");

    // The seeded versions are arranged so every wrong mapping picks a different answer: the
    // purpose point exists in the current version but is never reportable, the retired point is
    // reportable only in the superseded v1, the future point only in the not-yet-in-force v3.
    private static readonly Guid PurposePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000001");
    private static readonly Guid SpamPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");
    private static readonly Guid HatePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000003");
    private static readonly Guid RetiredPointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000004");
    private static readonly Guid FuturePointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000005");

    /// <summary>The instant every spec pins: v2 is in force (v1 superseded, v3 not yet).</summary>
    private static readonly DateTimeOffset Now = At("2026-07-01T12:00:00Z");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static StatuteVersion[] SeededVersions() =>
    [
        new(1, At("2025-01-01T00:00:00Z"),
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(PurposePointId, 1, "Podkop is a community for sharing findings. (v1)", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, "Do not post spam. (v1)", true),
                new StatutePoint(RetiredPointId, 2, "Do not post off-topic content. (v1)", true),
            ]),
        ]),
        new(2, At("2026-06-01T00:00:00Z"),
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(PurposePointId, 1, "Podkop is a community for sharing findings. (v2)", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, "Do not post spam. (v2)", true),
                new StatutePoint(HatePointId, 2, "Do not post hateful content. (v2)", true),
            ]),
        ]),
        new(3, At("2099-01-01T00:00:00Z"),
        [
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(SpamPointId, 1, "Do not post spam. (v3)", true),
                new StatutePoint(HatePointId, 2, "Do not post hateful content. (v3)", true),
                new StatutePoint(FuturePointId, 3, "Do not impersonate other users. (v3)", true),
            ]),
        ]),
    ];

    private static Finding CreateFinding(Guid id, string author) =>
        new(
            id: id,
            title: "A finding under scrutiny",
            description: "The finding the report targets.",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: author,
            tags: ["angular"],
            createdAt: At("2026-06-08T03:30:00Z"),
            promotedAt: At("2026-06-08T09:30:00Z"),
            commentCount: 0);

    private static WebApplicationFactory<Program> CreateFactory(DateTimeOffset? now = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(now ?? Now));
                services.AddSingleton<IStatuteRepository>(new InMemoryStatuteRepository(SeededVersions()));
                services.AddSingleton<IFindingRepository>(new InMemoryFindingRepository(
                    [CreateFinding(FindingId, "grace_hopper")]));
                services.AddSingleton<ICommentRepository>(provider => new InMemoryCommentRepository(
                [
                    new Comment(CommentId, FindingId, null, "grace_hopper",
                        "A comment under scrutiny.", At("2026-06-08T10:00:00Z")),
                    new Comment(ReplyId, FindingId, CommentId, "linus_torvalds",
                        "A reply under scrutiny.", At("2026-06-08T11:00:00Z")),
                ], provider.GetRequiredService<IPublisher>()));
            }));

    [Fact]
    public async Task The_statute_lookup_answers_the_version_in_force_and_only_its_reportable_points()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IStatuteLookup>();

        var current = await lookup.GetCurrentAsync(CancellationToken.None);

        Assert.NotNull(current);
        // The version in force at the pinned instant — not the oldest (1), not the highest (3).
        Assert.Equal(2, current.Version);
        // Only v2's reportable points: no never-reportable purpose point, no retired v1 point,
        // no not-yet-in-force v3 point.
        Assert.Equal([SpamPointId, HatePointId], current.ReportablePointIds);
    }

    [Fact]
    public async Task The_statute_lookup_answers_null_when_no_version_is_in_force()
    {
        using var factory = CreateFactory(At("2024-12-31T23:59:59Z"));
        using var scope = factory.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IStatuteLookup>();

        var current = await lookup.GetCurrentAsync(CancellationToken.None);

        Assert.Null(current);
    }

    [Fact]
    public async Task The_target_lookup_answers_a_findings_id_and_author()
    {
        using var factory = CreateFactory();
        var lookup = factory.Services.GetRequiredService<IReportTargetLookup>();

        var target = await lookup.GetAsync(ReportTargetKind.Finding, FindingId, CancellationToken.None);

        Assert.NotNull(target);
        Assert.Equal(FindingId, target.Id);
        Assert.Equal("grace_hopper", target.Author);
    }

    [Fact]
    public async Task The_target_lookup_answers_null_for_an_unknown_finding()
    {
        using var factory = CreateFactory();
        var lookup = factory.Services.GetRequiredService<IReportTargetLookup>();

        var target = await lookup.GetAsync(ReportTargetKind.Finding,
            Guid.Parse("0d4f9a3e-9999-4222-8333-444455556666"), CancellationToken.None);

        Assert.Null(target);
    }

    [Fact]
    public async Task The_target_lookup_answers_a_comments_id_and_author_replies_included()
    {
        using var factory = CreateFactory();
        var lookup = factory.Services.GetRequiredService<IReportTargetLookup>();

        var target = await lookup.GetAsync(ReportTargetKind.Comment, ReplyId, CancellationToken.None);

        Assert.NotNull(target);
        Assert.Equal(ReplyId, target.Id);
        // The reply's own author — the input to the self-report rule — not the thread's.
        Assert.Equal("linus_torvalds", target.Author);
    }

    [Fact]
    public async Task The_target_lookup_answers_null_for_an_unknown_comment()
    {
        using var factory = CreateFactory();
        var lookup = factory.Services.GetRequiredService<IReportTargetLookup>();

        // A finding's id is not a comment: the kinds resolve against different slices, so an id
        // that exists as a finding must still be unknown to the Comment kind.
        var target = await lookup.GetAsync(ReportTargetKind.Comment, FindingId, CancellationToken.None);

        Assert.Null(target);
    }

    [Fact]
    public async Task The_comments_lookup_answers_every_comment_and_reply_id_of_the_finding()
    {
        using var factory = CreateFactory();
        var lookup = factory.Services.GetRequiredService<IFindingCommentsLookup>();

        var commentIds = await lookup.GetCommentIdsAsync(FindingId, CancellationToken.None);

        Assert.NotNull(commentIds);
        Assert.Equal([CommentId, ReplyId], commentIds);
    }

    [Fact]
    public async Task The_comments_lookup_answers_null_for_an_unknown_finding()
    {
        using var factory = CreateFactory();
        var lookup = factory.Services.GetRequiredService<IFindingCommentsLookup>();

        var commentIds = await lookup.GetCommentIdsAsync(
            Guid.Parse("0d4f9a3e-9999-4222-8333-444455556666"), CancellationToken.None);

        Assert.Null(commentIds);
    }
}
