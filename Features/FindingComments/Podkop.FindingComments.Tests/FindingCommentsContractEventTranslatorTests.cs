using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Shared.Domain;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     What this slice lets the rest of the system hear about (issue #94, ADR 0014). The
///     translator is the slice's whole public vocabulary in one place: a comment having been
///     posted becomes the primitive-only <see cref="CommentPosted" /> the Findings slice already
///     consumes, and anything the slice records for its own purposes becomes nothing at all — so
///     no outbox row is written for it and no consumer is woken by a slice's private business.
/// </summary>
public class FindingCommentsContractEventTranslatorTests
{
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000101");
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    private readonly FindingCommentsContractEventTranslator _translator = new();

    [Fact]
    public void A_posted_comment_is_announced_as_CommentPosted_carrying_the_same_facts()
    {
        var announcement = _translator.Translate(new CommentAdded(CommentId, FindingId));

        var posted = Assert.IsType<CommentPosted>(announcement);
        Assert.Equal(CommentId, posted.CommentId);
        Assert.Equal(FindingId, posted.FindingId);
    }

    [Fact]
    public void An_event_the_slice_keeps_to_itself_is_announced_as_nothing()
    {
        // Nothing rather than an empty announcement: the outbox writes a row per answer it gets,
        // so a slice's private business must produce no answer at all or consumers are woken for
        // events that were never theirs to hear.
        Assert.Null(_translator.Translate(new SomethingTheSliceKeepsToItself()));
    }

    /// <summary>Stands in for any domain event that is nobody else's business.</summary>
    private sealed record SomethingTheSliceKeepsToItself : IDomainEvent;
}
