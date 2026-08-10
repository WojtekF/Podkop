# Bury and Report are fully separate concepts

A bury (with its Bury Reason) is a voting signal that counts against promotion; a Report is a moderation signal citing a reportable Statute Point. Neither feeds the other: buries never reach the moderation queue, and reports never affect a score or promotion. This matches Wykop, where burying with a reason and reporting a violation are distinct actions.

## Considered Options

- **Auto-file a report when the bury reason is Spam / False information / Inappropriate content** — rejected: floods the moderation queue with low-intent reports and requires a bury-reason→statute-point mapping to maintain.
- **Unify the vocabularies (replace Bury Reasons with Statute Points)** — rejected: a breaking refactor of the voting slice, and wrong in substance — "Unsuitable" is a taste judgment about the Main Page, not a statute violation.
