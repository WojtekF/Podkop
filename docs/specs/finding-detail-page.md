# Specification: Finding detail page

Status: agreed 2026-07-22 (grilling session). Reference behavior: Wykop (ADR 0002). Domain terms: `CONTEXT.md`. Architecture: ADR 0003, ADR 0005.

## Overview

A page showing one finding in full, with dig/bury voting on the finding and a one-level-deep comment section with up/down voting on comments. Reached from the Main Page cards.

## Current user (no auth yet)

- The backend exposes a `CurrentUser` seam hardcoded to the username `ada_lovelace` — deliberately one of the five sample authors, so own-content rules are observable in the running app.
- All vote/comment writes act as this user. Real auth later replaces exactly this seam.

## Routing & navigation

- Route: `/finding/:id` (lazy-loaded). No title slug.
- From a Main Page finding card:
  - Clicking the **description** navigates to the detail page, scrolled to the top.
  - Clicking the **comment count** navigates with the `#comments` fragment; the viewport is positioned so the first comment is centered.
  - The **title** keeps linking to the external source (unchanged Wykop behavior).

## Finding detail view

Shows the full finding: title (links to source), source domain, full description, thumbnail (when present), author, tags, timestamps, dig/bury buttons, and the comment section.

- The dig button shows `digCount`. The bury button shows **no count** — bury totals are not public (Wykop behavior); `buryCount` is therefore not in the detail DTO at all.
- The current user's existing vote is visually highlighted on the buttons.

## Finding votes (dig / bury)

1. Dig when unvoted → vote recorded, count +1, dig button highlighted.
2. Clicking dig while already dug → removes the dig (toggle off), count −1.
3. Bury always opens the reason picker first; the bury lands only once a reason is chosen. Clicking bury while already buried → removes the bury.
4. Switching sides is one click: bury-while-dug removes the dig and applies the bury (with reason), and vice versa. No undo-first ceremony.
5. The author cannot dig or bury their own finding; the buttons are disabled on own findings.
6. Changing a bury reason = un-bury, then bury again with the new reason (no dedicated edit flow).

**Bury reasons** (closed enum, exactly these five, per Wykop): Duplicate, Spam, False information, Inappropriate content, Unsuitable. The reason is stored on the vote and never shown publicly.

## Comments

- Plain text only, 1–5,000 characters. No markdown, embeds, or mention *feature* (an `@name` is inert text).
- **Structure: exactly one level.** A top-level comment can have replies; a reply can never have replies. The API enforces this: a reply's parent must be a top-level comment.
- **Reply affordance on every comment** (Wykop behavior): replying to a reply opens the composer targeting the **same top-level parent**, prefilled with `@author ` of the comment being answered.
- **Ordering:** top-level comments best-first — net score (upvotes − downvotes) descending, ties oldest-first. Replies under a parent are chronological, oldest first. No user-facing sort switcher yet.
- **Comment votes:** upvote/downvote (terms reserved for comments; findings use dig/bury). Same mechanics as finding votes minus reasons: toggle off on repeat click, one-click side switch, no voting on own comments (buttons disabled), current user's vote highlighted. **Both counts are always visible.**
- A comment row shows: author, time, text, upvote button + count, downvote button + count, reply button. No avatars.
- `Finding.CommentCount` counts everything — top-level comments **and** replies.

## API

### Findings slice

| Endpoint | Behavior |
| --- | --- |
| `GET /api/findings/{id}` | Finding detail: summary fields + `createdAt` + `myVote` (`"dig"` \| `"bury"` \| `null`). `404` unknown id. No `buryCount`. |
| `PUT /api/findings/{id}/my-vote` | Body `{ "type": "dig" }` or `{ "type": "bury", "reason": "duplicate" \| "spam" \| "falseInformation" \| "inappropriateContent" \| "unsuitable" }`. Idempotent set-my-vote; covers fresh votes and side switches. `400` bury without reason, `400` voting on own finding, `404` unknown finding. |
| `DELETE /api/findings/{id}/my-vote` | Removes the current user's vote (toggle-off). |

### FindingComments slice

| Endpoint | Behavior |
| --- | --- |
| `GET /api/findings/{findingId}/comments` | All threads, best-first with chronological replies. No paging yet (flagged in TODO, not built). `404` unknown finding. |
| `POST /api/findings/{findingId}/comments` | Body `{ "text": "...", "parentCommentId": null \| "<id>" }` → `201` with the created comment. `400` empty text / text > 5,000 chars / parent is itself a reply; `404` unknown finding or parent. |
| `PUT /api/comments/{commentId}/my-vote` | Body `{ "direction": "up" \| "down" }`. Same semantics as finding votes; `400` on own comment, `404` unknown comment. |
| `DELETE /api/comments/{commentId}/my-vote` | Removes the current user's vote. |

All vote and comment mutations return the fresh counts and `myVote` for the affected resource — the frontend reconciles from responses (no refetch, no optimistic updates).

## Architecture

- New slice `Features/FindingComments` with six projects: `Podkop.FindingComments.{Domain,Application,Infrastructure,Server,Contracts,Tests}` (ADR 0005).
- `Comment` is its own aggregate referencing `FindingId` — never a collection on `Finding`.
- `Comment` raises a `CommentAdded` **domain event** (internal to the slice, pattern of `Finding.Promote`). Infrastructure translates it after persistence into a **contract event** (MediatR `INotification` with primitive facts) in `Podkop.FindingComments.Contracts`. A handler in the Findings slice increments `Finding.CommentCount`. Findings references only the Contracts project.
- The Findings slice gains the detail query and the vote commands on its existing aggregate.

## Seed data

- Seeded comments are the **authority** for comment counts: seed a handful of realistic threads per finding and adjust `SampleFindings` counts to match. `47 comments` on a card must never open an empty discussion.
- Pre-existing vote state for the stub user, expressed as rules over the generated set (sample data is random per startup): some findings already dug, at least one buried with a reason, and a scattering of comment up/downvotes — so highlighting is visible on first load.

## Frontend

- Feature folder `src/finding-detail/`: `finding-detail.ts` (component) + `finding-detail.store.ts` (SignalStore), HTTP services mirroring the backend slices (`finding-detail.service.ts`, `finding-comments.service.ts`), child components `comment-thread/`, `comment/`, `comment-composer/` (shared by top-level and reply usage). Bury reason picker via `MatMenu`.
- `FindingCard` gains the description and comment-count navigation affordances.
- New-comment composer sits at the **top** of the comments section; the reply composer appears inline under its thread.

### Loading & failure UX

- One spinner for the whole page; detail and comments load in parallel and the page renders when both have landed (no partial render).
- `404` → "Finding not found" state with a link to the Main Page, **no retry**. Other load failures → the existing error-with-retry pattern.
- Vote clicks and comment posts disable their control while in flight; counts update from the response. Failure → Material snackbar; state unchanged; composer text preserved.

## Out of scope (explicitly)

- Comment paging/lazy-loading (TODO), markdown/formatting/mentions as features, comment editing/deletion, sort switcher, avatars, real auth, showing bury counts or reasons publicly.
