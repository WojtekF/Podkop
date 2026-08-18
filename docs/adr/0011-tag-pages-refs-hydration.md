# Tag pages serve typed refs; content slices hydrate the cards

The Tags slice's tag-page endpoint returns an ordered page of typed content references, not full cards: its read model is a membership index — (tag, content type, content id, created-at) — built from the content slices' announce events (ADR 0009), and the frontend hydrates each page via batch-by-ids endpoints on the owning slices, rendering in the index's order. We chose this over denormalizing card data into Tags (live scores would demand a per-vote update event crossing slice boundaries, or visibly stale cards) and over composing server-side in the host (the composition root would grow feature logic reaching into slices' Application layers, against ADR 0003's grain).

## Consequences

- Announce events stay tiny — tag set, content type, content id, created-at — and carry both directions: tag-set edits and content deletion announce too, so the index shrinks and a tag whose last content vanishes returns to 404 (a tag exists only while content carries it).
- A tag page render costs one Tags call plus one batch call per content type present on the page — accepted.
- Joining the tag namespace obliges a content slice to expose a batch-by-ids query endpoint (Findings now, Microblog per its spec).
- Tag-page ordering can only use facts the announce event carries: Newest ships; Best waits for a deliberate score-propagation decision.
- Refs whose content has just vanished hydrate to nothing and are dropped from the rendered page — a briefly short page is accepted over cross-slice consistency machinery.
