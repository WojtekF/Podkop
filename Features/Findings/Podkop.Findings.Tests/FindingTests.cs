using System.Globalization;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Tests;

public class FindingTests
{
    private static readonly DateTimeOffset FirstPromotion =
        DateTimeOffset.Parse("2026-07-05T12:00:00Z", CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset SecondAttempt = FirstPromotion.AddHours(3);

    private static Finding CreateUnpromotedFinding() => new(
        id: Guid.NewGuid(),
        title: "Angular 22 signals deep dive",
        description: "A walkthrough of signal-based components.",
        source: new Uri("https://dev.to/articles/angular-signals"),
        thumbnail: null,
        author: "ada_lovelace",
        tags: ["angular", "webdev"],
        createdAt: FirstPromotion.AddHours(-8),
        promotedAt: null,
        commentCount: 4);

    [Fact]
    public void Promote_stamps_the_promotion_time_and_marks_the_finding_promoted()
    {
        var finding = CreateUnpromotedFinding();

        finding.Promote(FirstPromotion);

        Assert.True(finding.IsPromoted);
        Assert.Equal(FirstPromotion, finding.PromotedAt);
    }

    [Fact]
    public void Promote_raises_a_FindingPromoted_event_carrying_the_finding_id_and_time()
    {
        var finding = CreateUnpromotedFinding();

        finding.Promote(FirstPromotion);

        var promotedEvent = Assert.Single(finding.DomainEvents.OfType<FindingPromoted>());
        Assert.Equal(finding.Id, promotedEvent.FindingId);
        Assert.Equal(FirstPromotion, promotedEvent.PromotedAt);
    }

    [Fact]
    public void Promoting_an_already_promoted_finding_keeps_the_original_promotion_time()
    {
        var finding = CreateUnpromotedFinding();
        finding.Promote(FirstPromotion);

        finding.Promote(SecondAttempt);

        Assert.Equal(FirstPromotion, finding.PromotedAt);
    }

    [Fact]
    public void Promoting_an_already_promoted_finding_raises_no_second_event()
    {
        var finding = CreateUnpromotedFinding();
        finding.Promote(FirstPromotion);

        finding.Promote(SecondAttempt);

        Assert.Single(finding.DomainEvents.OfType<FindingPromoted>());
    }
}
