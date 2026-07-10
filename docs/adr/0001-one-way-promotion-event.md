# Promotion is a one-way recorded event, not a computed state

A finding is promoted from Upcoming to the Main Page when its net score (digs − buries) reaches a configurable threshold. Crossing the threshold fires a domain event and permanently stamps `promotedAt` on the finding; later buries never demote it (only future moderation tooling could). We chose this over evaluating `net score ≥ N` at query time because a computed state would let findings flicker on and off the Main Page as votes change, and would leave no promotion timestamp — which the Main Page needs as its sort key (newest-promoted first, matching Wykop).

## Considered Options

- **Computed at query time** — no extra state, but unstable feed membership and no `promotedAt` to sort by.
- **One-way with auto-demotion below a second threshold** — deviates from Wykop's observable behavior and adds a second rule to define, tune, and test.
