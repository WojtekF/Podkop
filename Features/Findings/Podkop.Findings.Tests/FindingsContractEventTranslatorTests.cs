using System.Globalization;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;
using Podkop.Shared.Domain;
using Podkop.Tags.Contracts;

namespace Podkop.Findings.Tests;

/// <summary>
///     What this slice lets the rest of the system hear about (issue #77, ADR 0014). The
///     translator is the slice's whole public vocabulary in one place: a finding's tag set and a
///     finding's removal become the primitive-only announcements the Tags slice indexes (ADR
///     0009/0011), and everything the slice records for its own purposes becomes nothing at all —
///     so no outbox row is written for it and no consumer is woken by a slice's private business.
/// </summary>
public class FindingsContractEventTranslatorTests
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-7777-4222-8333-444455556666");

    private readonly FindingsContractEventTranslator _translator = new();

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private TaggedContentAnnounced TranslateTagsChanged(params string[] tags) =>
        Assert.IsType<TaggedContentAnnounced>(
            _translator.Translate(new FindingTagsChanged(FindingId, tags, At("2026-07-01T06:00:00Z"))));

    [Fact]
    public void A_changed_tag_set_is_announced_as_TaggedContentAnnounced_carrying_the_same_facts()
    {
        var announced = TranslateTagsChanged("dotnet", "webdev");

        Assert.Equal(FindingId, announced.ContentId);
        Assert.Equal(["dotnet", "webdev"], announced.Tags);
        Assert.Equal(At("2026-07-01T06:00:00Z"), announced.CreatedAt);
    }

    [Fact]
    public void The_announcement_names_the_content_type_the_tag_namespace_knows()
    {
        // A primitive both slices agree on (ADR 0009): the Tags slice maps it to its own
        // vocabulary at its own edge, and neither slice sees the other's enum.
        Assert.Equal(TaggedContentTypes.Finding, TranslateTagsChanged("dotnet").ContentType);
    }

    [Fact]
    public void A_removed_finding_is_announced_as_TaggedContentRemoved()
    {
        var announcement = _translator.Translate(new FindingRemoved(FindingId));

        var removed = Assert.IsType<TaggedContentRemoved>(announcement);
        Assert.Equal(FindingId, removed.ContentId);
        Assert.Equal(TaggedContentTypes.Finding, removed.ContentType);
    }

    [Fact]
    public void An_announcement_carries_a_fresh_identity_of_its_own()
    {
        // Delivery is at-least-once (ADR 0014): consumers recognize a redelivered announcement by
        // its EventId, so an announcement without one — Guid.Empty is not one — can never be
        // deduplicated and would be acted on once per delivery.
        Assert.NotEqual(Guid.Empty, TranslateTagsChanged("dotnet").EventId);
        Assert.NotEqual(
            Guid.Empty,
            Assert.IsType<TaggedContentRemoved>(_translator.Translate(new FindingRemoved(FindingId))).EventId);
    }

    [Fact]
    public void Two_announcements_are_two_identities_even_of_the_same_fact()
    {
        // The identity belongs to the announcement, not to the fact it announces: deduplication
        // exists to swallow the same announcement delivered twice, never a genuine second one —
        // so it must not be derivable from the fact's own ids.
        Assert.NotEqual(TranslateTagsChanged("dotnet").EventId, TranslateTagsChanged("dotnet").EventId);
    }

    [Fact]
    public void A_promotion_is_announced_as_nothing()
    {
        // Promotion is an internal one-way fact (ADR 0001), and the tag index deliberately
        // carries no scores or surfaces (ADR 0011) — nobody outside this slice is waiting for it.
        Assert.Null(_translator.Translate(new FindingPromoted(FindingId, At("2026-07-08T09:30:00Z"))));
    }

    [Fact]
    public void An_event_the_slice_keeps_to_itself_is_announced_as_nothing()
    {
        // Nothing rather than an empty announcement: the outbox writes a row per answer it gets,
        // so a slice's private business must produce no answer at all.
        Assert.Null(_translator.Translate(new SomethingTheSliceKeepsToItself()));
    }

    /// <summary>Stands in for any domain event that is nobody else's business.</summary>
    private sealed record SomethingTheSliceKeepsToItself : IDomainEvent;
}
