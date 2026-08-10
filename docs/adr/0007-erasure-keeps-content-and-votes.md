# Erasure keeps content and votes, anonymized

GDPR erasure of a user keeps their findings and comments, attributed to the "Deleted Account" placeholder, and keeps their cast votes — no score or promotion outcome changes when an account dies. Surviving votes are re-keyed from the username to a non-identifying token unique per vote (a single shared placeholder key would collapse multiple erased users' votes inside one aggregate; keeping the username would retain personal data). Pending reports by the erased user are dropped; resolved ones stay in the moderation log anonymized.

## Considered Options

- **Erase everything they touched** (content, votes, reports) — rejected: destroys other users' threads under erased findings and shifts community scores retroactively; GDPR accepts anonymization for community content.
- **Rename only, keep username-keyed votes** — rejected: retains a linked behavioral trail (personal data) and corrupts per-voter uniqueness once two erased users share the placeholder key.
