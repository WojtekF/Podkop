using Podkop.Tags.Application;
using Podkop.Tags.Domain;

namespace Podkop.Tags.Infrastructure;

/// <summary>
///     The durable answer to <see cref="ITagMembershipRepository" /> (issue #77): the membership
///     index lives in the slice's PostgreSQL schema, reached through <see cref="TagsDbContext" />.
///     The page is composed by the database — one tag's rows, narrowed to a content type when one
///     is asked for, newest created-at first with content id descending as the tiebreak, the pages
///     before the asked-for one skipped, and up to one row beyond the limit as the next-page
///     signal — never by loading a tag's whole history and paging in memory (ADR 0004). The
///     existence question asks the database whether the tag is carried at all, without dragging
///     its rows back. Loaded rows stay tracked by the request's context: the repository persists
///     nothing itself — durability is <see cref="EfUnitOfWork" />'s single explicit commit. The
///     specs in <c>Podkop.Tags.Tests</c> pin all of this against the live database.
/// </summary>
public sealed class EfTagMembershipRepository(TagsDbContext context) : ITagMembershipRepository
{
    public Task<bool> AnyContentCarriesAsync(string tag, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<TagMembership>> GetPageAsync(
        string tag,
        TaggedContentType? contentType,
        int page,
        int limit,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<TagMembership>> GetForContentAsync(
        TaggedContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void Add(TagMembership membership) => throw new NotImplementedException();

    public void RemoveRange(IEnumerable<TagMembership> memberships) => throw new NotImplementedException();
}
