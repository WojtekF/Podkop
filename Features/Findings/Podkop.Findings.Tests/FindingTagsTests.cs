using System.Globalization;
using Podkop.Findings.Domain;

namespace Podkop.Findings.Tests;

/// <summary>
///     The Findings side of the tag namespace (issue #77, ADR 0009): a finding's tags are set
///     through one write-time seam that folds them into the platform's one canonical form, and
///     every change to the set — and the finding going away — is announced, so the Tags slice can
///     keep its index true. Pure domain specs: no database, no HTTP.
/// </summary>
public class FindingTagsTests
{
    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(params string[] tags) =>
        new(
            id: Guid.Parse("0d4f9a3e-7777-4222-8333-444455556666"),
            title: "A tagged finding",
            description: "A tagged finding — description",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: tags,
            createdAt: At("2026-07-01T06:00:00Z"),
            promotedAt: null,
            commentCount: 0,
            votes: null);

    private static FindingTagsChanged SingleTagsChanged(Finding finding) =>
        Assert.Single(finding.DomainEvents.OfType<FindingTagsChanged>());

    [Fact]
    public void Setting_tags_folds_them_into_the_platforms_canonical_form()
    {
        var finding = CreateFinding();

        finding.SetTags(["DotNet", "web-dev", "Wszechświat"]);

        Assert.Equal(["dotnet", "webdev", "wszechswiat"], finding.Tags);
    }

    [Fact]
    public void Setting_tags_replaces_the_whole_set_rather_than_adding_to_it()
    {
        var finding = CreateFinding("dotnet", "webdev");

        finding.SetTags(["aspire"]);

        Assert.Equal(["aspire"], finding.Tags);
    }

    [Fact]
    public void A_tag_naming_nothing_canonical_is_dropped_rather_than_carried()
    {
        var finding = CreateFinding();

        finding.SetTags(["dotnet", "---", "  "]);

        Assert.Equal(["dotnet"], finding.Tags);
    }

    [Fact]
    public void Tags_that_fold_to_the_same_thing_are_carried_once()
    {
        // Two spellings of one tag are one tag (ADR 0009), so a finding cannot join it twice —
        // otherwise the announcement files a duplicate row in the index.
        var finding = CreateFinding();

        finding.SetTags(["DotNet", "dotnet", "dot-net"]);

        Assert.Equal(["dotnet"], finding.Tags);
    }

    [Fact]
    public void Setting_tags_announces_the_whole_resulting_set()
    {
        var finding = CreateFinding();

        finding.SetTags(["DotNet", "webdev"]);

        var announced = SingleTagsChanged(finding);
        Assert.Equal(finding.Id, announced.FindingId);
        Assert.Equal(["dotnet", "webdev"], announced.Tags);
    }

    [Fact]
    public void The_announcement_carries_the_findings_creation_time_not_the_edits()
    {
        // ADR 0011: created-at is what a tag page orders by, so re-tagging an old finding must
        // never jump it to the top of one.
        var finding = CreateFinding("dotnet");

        finding.SetTags(["aspire"]);

        Assert.Equal(finding.CreatedAt, SingleTagsChanged(finding).CreatedAt);
    }

    [Fact]
    public void Setting_the_same_set_again_announces_nothing()
    {
        // Nothing changed, so the index has nothing to hear — and an announcement per no-op edit
        // is delivery traffic bought for nothing.
        var finding = CreateFinding("dotnet", "webdev");

        finding.SetTags(["DotNet", "webdev"]);

        Assert.Empty(finding.DomainEvents.OfType<FindingTagsChanged>());
    }

    [Fact]
    public void Removal_announces_that_the_finding_is_gone()
    {
        var finding = CreateFinding("dotnet");

        finding.Remove();

        Assert.Equal(finding.Id, Assert.Single(finding.DomainEvents.OfType<FindingRemoved>()).FindingId);
    }
}
