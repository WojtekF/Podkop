# Tags is its own slice, and contract projects may carry shared value types

Tags span content slices: findings and (coming) Microblog entries share one tag namespace and one combined per-tag stream, and every content slice must apply the same canonical-form rule (fold input to lowercase `[a-z0-9]`, 1–50 characters) at write time. We decided a dedicated Tags slice owns the tag model and the tag pages, and — extending ADR 0003, whose contract projects previously held only event records — `Podkop.Tags.Contracts` publishes the Tag value type with its normalization for content slices to use when accepting tagged content. Content slices keep announcing tagged content through their own contract events, which the Tags slice indexes into its combined stream; Tags references no content slice, and content slices reference only Tags' contracts.

We chose this over leaving the rule in Findings (the sharing question would just reopen when Microblog arrives) and over duplicating the rule per slice (drift between copies of the canonical form would fork the namespace). A Tag stays a value object — no Tag store or identity exists until a feature that needs one (observation, author tags) arrives.

## Consequences

- ADR 0003's "contracts hold only public event records" is extended: a Contracts project may also carry shared value types when slices must agree on a value's canonical form. Events remain the only cross-slice communication; value contracts share vocabulary, never behavior beyond it.
- The dependency direction stays acyclic: content slices → `Podkop.Tags.Contracts`; the Tags slice consumes content slices' contract events and references no content slice.
- Tag observation and author tags ("tagi autorskie") are deliberately unmodeled: observation belongs to the profiles + social epic, author tags to a future effort.
