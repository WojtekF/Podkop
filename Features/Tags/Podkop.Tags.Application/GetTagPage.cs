using MediatR;
using Podkop.Tags.Contracts;

namespace Podkop.Tags.Application;

/// <summary>
///     Which content types a tag page lists: the combined stream every content type shares, or
///     one type on its own. Full from day one (the tag-page spec, issue #77) — Entries is a
///     legitimate, empty view until the Microblog slice lands, and lights up with no rework.
/// </summary>
public enum TagContentFilter
{
    All,
    Findings,
    Entries
}

/// <summary>
///     Query for one page of a Tag Page's stream: the content carrying the named tag, newest
///     created-at first, narrowed by <paramref name="Filter" />, addressed by a 1-based page
///     number (ADR 0004).
///     <para>
///         <paramref name="Name" /> arrives exactly as the URL spelled it and is folded through
///         <see cref="Tag" /> here, so any casing or variant resolves to the canonical tag and
///         one page answers them all. The query yields <c>null</c> — which the endpoint turns
///         into a 404 — when the name names no tag that exists: either it folds to nothing at
///         all, or it folds to a tag no content carries, whether because it never existed or
///         because its last content has since vanished (ADR 0011: a tag exists exactly as long as
///         content carries it). A tag that exists but whose asked-for page or type filter is
///         empty is <b>not</b> that case: it answers an empty page, so a stale deep link
///         degrades gracefully (ADR 0004) and narrowing to a type carrying nothing stays a view
///         of a real tag.
///     </para>
/// </summary>
public sealed record GetTagPage(string Name, TagContentFilter Filter, int Page, int Limit)
    : IRequest<TagPage?>;

/// <summary>
///     One page of typed references, in the index's order (ADR 0011). No card data: the frontend
///     hydrates each type through the owning slice's batch-by-ids endpoint and renders in this
///     order.
/// </summary>
public sealed record TagPage(IReadOnlyList<TaggedContentRef> Items, bool HasNextPage);

/// <summary>One reference: what type of content, and which one.</summary>
public sealed record TaggedContentRef(string Type, Guid Id);

public sealed class GetTagPageHandler(ITagMembershipRepository memberships)
    : IRequestHandler<GetTagPage, TagPage?>
{
    public Task<TagPage?> Handle(GetTagPage request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
