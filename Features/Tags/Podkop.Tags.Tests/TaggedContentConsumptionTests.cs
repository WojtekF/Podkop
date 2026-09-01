using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Application;
using Podkop.Tags.Contracts;
using Podkop.Tags.Domain;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     How the membership index is built and unbuilt (ADR 0011): by consuming content slices'
///     announce events, in both directions. An announcement replaces a piece of content's whole
///     membership, so a tag dropped from an edited set really leaves that tag's page; a removal
///     takes the content out entirely, so a tag whose last content vanished can return to 404.
///     <para>
///         Delivery is at-least-once (ADR 0014), so each announcement is acted on exactly once —
///         recognized by the EventId its producer stamped, through the slice's inbox, and recorded
///         in the same commit as the index change it guards. Each delivery runs in a scope of its
///         own over this slice's real schema, the way the processor's publisher resolves a fresh
///         handler per event.
///     </para>
/// </summary>
[Collection(TagsDatabaseCollection.Name)]
public class TaggedContentConsumptionTests(TagsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid ContentId = Guid.Parse("0d4f9a3e-7777-4222-8333-444455556666");
    private static readonly Guid OtherContentId = Guid.Parse("0d4f9a3e-7777-4222-8333-999999999999");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static TaggedContentAnnounced Announced(
        string eventId,
        string[] tags,
        Guid? contentId = null,
        string contentType = TaggedContentTypes.Finding,
        string createdAt = "2026-07-08T10:00:00Z") =>
        new(Guid.Parse(eventId), contentType, contentId ?? ContentId, tags, At(createdAt));

    private static TaggedContentRemoved Removed(
        string eventId, Guid? contentId = null, string contentType = TaggedContentTypes.Finding) =>
        new(Guid.Parse(eventId), contentType, contentId ?? ContentId);

    /// <summary>
    ///     One delivery's worth of work, wired the way the publisher's scope wires it: handler,
    ///     repository, unit of work, and inbox all over the same fresh context, so what this
    ///     delivery did — and remembered doing — is one commit.
    /// </summary>
    private async Task Delivered(TaggedContentAnnounced announcement)
    {
        await using var context = database.CreateDbContext();
        var handler = new TaggedContentAnnouncedHandler(
            new EfTagMembershipRepository(context),
            new EfUnitOfWork(context),
            new EfInbox(context, TimeProvider.System));
        await handler.Handle(announcement, CancellationToken.None);
    }

    private async Task Delivered(TaggedContentRemoved removal)
    {
        await using var context = database.CreateDbContext();
        var handler = new TaggedContentRemovedHandler(
            new EfTagMembershipRepository(context),
            new EfUnitOfWork(context),
            new EfInbox(context, TimeProvider.System));
        await handler.Handle(removal, CancellationToken.None);
    }

    private async Task<IReadOnlyList<TagMembership>> IndexAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.TagMemberships.AsNoTracking().ToListAsync();
    }

    private async Task<string[]> TagsOf(Guid contentId)
    {
        var index = await IndexAsync();
        return [.. index.Where(row => row.ContentId == contentId).Select(row => row.Tag).Order()];
    }

    /// <summary>The slice's memory of what it has acted on, read back from its own schema.</summary>
    private async Task<IReadOnlyList<InboxMessage>> ConsumedAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.InboxMessages.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task An_announcement_files_the_content_under_every_tag_it_carries()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet", "webdev"]));

        Assert.Equal(["dotnet", "webdev"], await TagsOf(ContentId));
    }

    [Fact]
    public async Task A_filed_row_carries_the_contents_own_creation_time_not_the_announcements()
    {
        // ADR 0011: created-at is what the tag page orders by, so it has to be the content's,
        // never the moment the index heard about it.
        await Delivered(Announced(
            "e0000000-0000-4000-8000-000000000001", ["dotnet"], createdAt: "2026-06-01T08:30:00Z"));

        var row = Assert.Single(await IndexAsync());
        Assert.Equal(At("2026-06-01T08:30:00Z"), row.CreatedAt);
        Assert.Equal(TaggedContentType.Finding, row.ContentType);
        Assert.Equal(ContentId, row.ContentId);
    }

    [Fact]
    public async Task The_announced_tags_are_folded_to_their_canonical_form_before_filing()
    {
        // Producers fold at write time (ADR 0009), but the index is the namespace's own
        // guarantee: a row spelled any other way would be a second, invisible tag.
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["DotNet", "web-dev"]));

        Assert.Equal(["dotnet", "webdev"], await TagsOf(ContentId));
    }

    [Fact]
    public async Task An_announced_tag_that_folds_to_nothing_files_no_row()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet", "---"]));

        Assert.Equal(["dotnet"], await TagsOf(ContentId));
    }

    [Fact]
    public async Task A_later_announcement_replaces_the_whole_tag_set_rather_than_adding_to_it()
    {
        // The edit case, and the reason announcements carry the whole set: dropping a tag has to
        // take the content off that tag's page, which only a replacement can do.
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet", "webdev"]));

        await Delivered(Announced("e0000000-0000-4000-8000-000000000002", ["dotnet", "aspire"]));

        Assert.Equal(["aspire", "dotnet"], await TagsOf(ContentId));
    }

    [Fact]
    public async Task A_replacement_leaves_other_contents_rows_alone()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));
        await Delivered(Announced(
            "e0000000-0000-4000-8000-000000000002", ["dotnet"], contentId: OtherContentId));

        await Delivered(Announced("e0000000-0000-4000-8000-000000000003", ["aspire"]));

        Assert.Equal(["dotnet"], await TagsOf(OtherContentId));
    }

    [Fact]
    public async Task Two_content_types_may_carry_the_same_tag_without_colliding()
    {
        // One namespace shared by every content type (CONTEXT.md): the same tag on a finding and
        // on an entry with the same id would still be two rows.
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));

        await Delivered(Announced(
            "e0000000-0000-4000-8000-000000000002", ["dotnet"], contentType: TaggedContentTypes.Entry));

        var index = await IndexAsync();
        Assert.Equal(2, index.Count);
        Assert.Equal(
            [TaggedContentType.Entry, TaggedContentType.Finding],
            index.Select(row => row.ContentType).Order().ToArray());
    }

    [Fact]
    public async Task An_announcement_naming_a_content_type_the_namespace_does_not_carry_files_nothing()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"], contentType: "photo"));

        Assert.Empty(await IndexAsync());
    }

    [Fact]
    public async Task A_first_delivery_files_the_content_and_remembers_the_announcement()
    {
        var announcement = Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]);

        await Delivered(announcement);

        Assert.Equal(["dotnet"], await TagsOf(ContentId));
        // The memory and the index change commit together — an effect the slice cannot remember
        // causing would be repeated on redelivery.
        Assert.Equal(announcement.EventId, Assert.Single(await ConsumedAsync()).Id);
    }

    [Fact]
    public async Task A_redelivered_announcement_changes_nothing()
    {
        // At-least-once delivery in its plainest form: the processor published, crashed before
        // marking the row, and published again — same EventId, same facts, two deliveries.
        var announcement = Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]);

        await Delivered(announcement);
        await Delivered(announcement);

        Assert.Single(await IndexAsync());
        Assert.Single(await ConsumedAsync());
    }

    [Fact]
    public async Task A_redelivered_announcement_cannot_undo_a_later_one()
    {
        // The redelivery that actually costs something: without the inbox, replaying the first
        // announcement would put the dropped tag back and take the added one away.
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet", "webdev"]));
        await Delivered(Announced("e0000000-0000-4000-8000-000000000002", ["aspire"]));

        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet", "webdev"]));

        Assert.Equal(["aspire"], await TagsOf(ContentId));
    }

    [Fact]
    public async Task A_removal_takes_the_content_out_of_every_tag_it_was_under()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet", "webdev", "aspire"]));

        await Delivered(Removed("e0000000-0000-4000-8000-000000000002"));

        Assert.Empty(await TagsOf(ContentId));
    }

    [Fact]
    public async Task A_removal_leaves_other_contents_rows_alone()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));
        await Delivered(Announced(
            "e0000000-0000-4000-8000-000000000002", ["dotnet"], contentId: OtherContentId));

        await Delivered(Removed("e0000000-0000-4000-8000-000000000003"));

        Assert.Equal(["dotnet"], await TagsOf(OtherContentId));
    }

    [Fact]
    public async Task A_removal_only_takes_out_the_content_type_it_names()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));
        await Delivered(Announced(
            "e0000000-0000-4000-8000-000000000002", ["dotnet"], contentType: TaggedContentTypes.Entry));

        await Delivered(Removed("e0000000-0000-4000-8000-000000000003", contentType: TaggedContentTypes.Entry));

        var row = Assert.Single(await IndexAsync());
        Assert.Equal(TaggedContentType.Finding, row.ContentType);
    }

    [Fact]
    public async Task A_removal_of_content_that_was_never_indexed_changes_nothing_and_does_not_fail()
    {
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));

        await Delivered(Removed("e0000000-0000-4000-8000-000000000002", contentId: OtherContentId));

        Assert.Equal(["dotnet"], await TagsOf(ContentId));
    }

    [Fact]
    public async Task A_first_removal_is_remembered_like_any_other_announcement()
    {
        var removal = Removed("e0000000-0000-4000-8000-000000000002");
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));

        await Delivered(removal);

        Assert.Contains(removal.EventId, (await ConsumedAsync()).Select(message => message.Id));
    }

    [Fact]
    public async Task A_redelivered_removal_cannot_undo_a_later_announcement()
    {
        // The finding came back — a resubmission, say — and a stray redelivery of the old
        // removal must not take it off its tag pages again.
        await Delivered(Announced("e0000000-0000-4000-8000-000000000001", ["dotnet"]));
        var removal = Removed("e0000000-0000-4000-8000-000000000002");
        await Delivered(removal);
        await Delivered(Announced("e0000000-0000-4000-8000-000000000003", ["dotnet"]));

        await Delivered(removal);

        Assert.Equal(["dotnet"], await TagsOf(ContentId));
    }
}
