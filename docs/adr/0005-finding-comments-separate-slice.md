# Comments live in their own FindingComments slice, not inside Findings

Comments on findings are implemented as a separate vertical slice, `Features/FindingComments`, rather than folded into the existing Findings slice — even though a comment cannot exist without its finding. Existential dependency argues only for a foreign key and cascade delete, not for co-located code: no Finding invariant reads comments in the same transaction (promotion, dig/bury, and reply-depth rules never cross the line), a finding's comments are paged and voted on independently at Wykop scale, and the comment use cases (add, reply, vote, list) form a cohesive cluster that shares nothing with feed/promotion logic except a `FindingId`. Folding them in would start the slide toward one giant Findings feature, since on a Wykop-style site everything hangs off a finding.

The slice is named **FindingComments** (not Comments) to say what it comments on, leaving room for other commentable things later.

The one fact that crosses the boundary — `Finding.CommentCount`, a denormalized display counter — is synchronized eventually: the Comment aggregate raises a `CommentAdded` domain event, Infrastructure translates it after persistence into a public contract event in `Podkop.FindingComments.Contracts` (the optional Contracts layer from ADR 0003), and a handler in the Findings slice increments its count.

## Consequences

- Five more projects (plus Contracts) for a prototype-stage feature — accepted ceremony, per ADR 0003's direction.
- `CommentCount` is eventually consistent with the actual comments; nobody files a bug because a count lagged by one for a moment.
- Comment is its own aggregate referencing `FindingId`; it is never held as a collection on `Finding`.
