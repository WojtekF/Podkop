# Comments live in their own FindingComments slice, not inside Findings

Comments on findings are a separate vertical slice, `Features/FindingComments`, rather than folded into the Findings slice — even though a comment cannot exist without its finding. Existential dependency argues only for a foreign key and cascade delete: no Finding invariant reads comments in the same transaction, comments are voted on independently and grow unboundedly, and the comment use cases (add, reply, vote, list) share nothing with feed/promotion logic except a `FindingId`. The slice is named **FindingComments** (not Comments) to say what it comments on, leaving room for other commentable things later.

## Consequences

- Six projects for a prototype-stage feature — accepted ceremony, per ADR 0003's direction.
- `Finding.CommentCount` — the one fact that crosses the boundary — is synchronized eventually via the contract-event pattern (ADR 0003), so it can briefly lag the actual comments.
- Comment is its own aggregate referencing `FindingId`; it is never held as a collection on `Finding`.
