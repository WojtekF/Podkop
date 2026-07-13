# Feeds paginate by page number, not cursor

`GET /api/findings` selects a feed with `feed=` and pages it with a 1-based `page` and a `limit` (default 25, max 100), returning `{ items, hasNextPage }`; the earlier opaque forward-only cursor (`FeedCursor`) is removed. The Main Page UX is Wykop-style Previous/Next paging (per ADR-0002), which needs random access to earlier pages — a forward-only cursor cannot serve a Previous button without client-side cursor bookkeeping, and bidirectional cursors add complexity page numbers avoid. Page numbers also make deep links shareable (`?page=3`).

## Consequences

- Pages can shift under the reader as findings are promoted or scores change (an item may repeat or be missed across a page turn) — accepted; Wykop behaves the same.
- Requests past the last page return `200` with `{ items: [], hasNextPage: false }`, so stale deep links degrade gracefully rather than 404.
- Offset-style paging can get slow at depth once real persistence lands; if that bites, revisit via caching or keyset techniques (issue #6 explores one option) without changing the public contract.
