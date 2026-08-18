# Wykop.pl: Mikroblog — observed behavior

- **Purpose:** observable-behavior reference for ADR 0002's 1:1 copy of Wykop; primary input for the Microblog boundary-and-language decision session (ticket #55). Resolves research ticket #52.
- **Research date:** 2026-08-18
- **Method / constraint:** anonymous (logged-out) access only, via live wykop.pl pages (server-rendered HTML fetched directly), the official FAQ pages on wykop.pl, and Wykop's official API v3 documentation at doc.wykop.pl (a Wykop-owned primary source; useful because it states numeric limits the FAQ omits). No login, no account, nothing submitted or voted. Login-gated behavior is recorded as an open question instead. One web search was used only to *locate* spoiler-bearing entry permalinks, which were then loaded directly from wykop.pl.
- **Consent handling:** no consent was granted; all pages were read from server-rendered HTML without executing the site's scripts, so the TCF consent dialog (documented in the sibling findings research) was never interacted with.
- **Rendering note:** wykop.pl is a Vue SPA with server-side rendering. All DOM claims below come from the server-rendered HTML of the cited URLs. Some UI (dropdown option lists, search results) hydrates client-side only and is noted as such where it matters.
- **Labeling:** OBSERVED = seen in a public page's rendered output/DOM; DOCUMENTED = stated by Wykop's FAQ or official API docs; claims prefixed "Inference:" are neither. Polish UI terms are given in parentheses on first use.
- **Sibling doc:** `docs/research/wykop-finding-submission-and-tags.md` (same method, same date) covers the findings side; shared facts (tag pages, consent dialog, login colors) are cross-referenced rather than re-derived.

---

## TL;DR

- An entry (wpis) is free-form text 5–20 000 chars (DOCUMENTED, API) plus optionally **one** photo, **one** external embed, **one** poll (ankieta), and an 18+ (adult) flag; tags and @-mentions live inline in the body — there is no separate tag field.
- Body supports formatting: `**bold**`, `__…__`, `[label](url)` links (DOCUMENTED, API example), quotes rendered as `<blockquote>`, and a spoiler syntax — a line prefixed with `!` renders as a "Pokaż spoiler" button (OBSERVED).
- Entry voting is **plus-only** ("plusowanie"): one "+" button, no minus, no bury (zakop), no downvote reasons — the asymmetry with findings is real. A vote can be retracted; authors cannot plus their own entry (DOCUMENTED, API).
- Entry comments are **flat** (one level, no threading); replying is done by "@username:" convention. Comment voting is also plus-only. Comment lists are chronological (oldest first) with **no sort selector** — unlike finding comments, which have three sorts.
- The comment box can be media-only: comment text may be empty if a photo/embed is attached, otherwise min 5 chars (DOCUMENTED, API).
- Mikroblog is a top-level main-nav section (Główna | Wykopalisko | Hity | Mikroblog). `/mikroblog` defaults to hot ("gorące") with 2h/6h/12h/24h windows; "najnowsze" (newest) and "aktywne" (active) views exist as sub-URLs.
- Reaching hot is engagement-based (votes + comment activity), not a fixed threshold (DOCUMENTED, FAQ). ~2 hot entries were interleaved among 25 finding cards on the homepage main stream (OBSERVED).
- Tags are one shared namespace with findings: tag pages default to a combined stream with a type filter Wszystko/Znaleziska/Wpisy (`/tag/{name}/wpisy` for entries-only).
- Polls: question 5–100 chars, 2–10 answers, ≤50 chars each (DOCUMENTED, API); anonymous visitors see full results ("Oddanych głosów: N") but no vote control (OBSERVED). Polls exist only on entries, never on comments.
- Entry authors moderate their own discussions — they may remove other users' comments under their entry (DOCUMENTED, FAQ, verbatim: "Autorzy Wpisów mają możliwość moderowania własnych dyskusji na Mikroblogu").
- Editing: 15 minutes for the entry per the API docs (the FAQ states 15 min only for findings and 10 min for comments, and is silent on entries); deleting one's own entry is allowed **without** a time limit (DOCUMENTED, API).
- Rate limits by login color: 15/20/30 entries per 30 min, mentions of 20/50/150 people (DOCUMENTED, FAQ).

---

## 1. What an entry (wpis) is

### Official definition

- DOCUMENTED (https://wykop.pl/faq/definicje, "Mikroblog", paraphrased): the Mikroblog is the place to discuss any topic freely by adding entries (Wpisy) — share how your day went, ask for advice, or just talk; the most interesting/funniest content reaches the so-called hot section ("Gorące"). "Wpis" has no separate FAQ definition beyond this.

### Composition and limits

DOCUMENTED — official API v3 create/update schema (https://doc.wykop.pl/components/schemas/create_update_entry.yaml, served by the Swagger index at https://doc.wykop.pl/):

- `content` — the user's text, **min 5 / max 20 000 characters** (validation errors `too_short` / `too_long`). The schema's own example shows the formatting vocabulary: `**foobar** __foobar__ [lorem](https://www.wykop.pl) impsum!!! #nsfw #wykop` — i.e. bold, underscore emphasis, Markdown-style links, and tags written inline in the body.
- `photo` — **one** image attachment, referenced by an upload key (uploads must use the `comments` file area).
- `embed` — **one** external embed, referenced by key.
- `survey` — **one** poll, referenced by id (see §1.5).
- `adult` — boolean "entry for adults only" flag (see §6.2).
- There is **no title field and no separate tags field** — an entry is body + attachments; tags and mentions are part of the text.

### 1.1 Text formatting as rendered (OBSERVED)

On the entry permalink https://wykop.pl/wpis/87290753/glowni-i-wulgarni-ludzie-wszedzie-psy-wariaci-drog and streams:

- Tags render inline as `#` + anchor to `/tag/{name}` (e.g. `#<a href="/tag/got">got</a>`). One entry with 11 tags was observed on /tag/polska (sibling doc §3), so entries are not held to the 2–6 tag range findings exhibit.
- @-mentions render as a literal `@` followed by an anchor to `/ludzie/{username}`. Mention ("przywołanie") limits per login color: green 20, orange 50, burgundy 150 people (DOCUMENTED, https://wykop.pl/faq/konto).
- Quotes render as `<blockquote>` blocks (OBSERVED in comments on the poll entry above).
- **Spoiler**: an entry whose source text begins with `!` (visible verbatim in the page's own `og:title` meta: "! Ja wiem, rzeczy niezrozumiałych jest dużo…") renders that text hidden behind a `section.content-spoiler` with a button labelled **"Pokaż spoiler"** (show spoiler); the page CSS renders revealed spoiler text in monospace. OBSERVED on https://wykop.pl/wpis/26122617/pokaz-spoiler-got (a 2017 entry still fully rendered today). Search-located sibling examples show the community phrase "Ukryty tekst" (hidden text) for the same mechanism.
- Long bodies truncate in streams behind **"Pokaż całość"** (show all) (OBSERVED on /mikroblog, /tag/polska, profiles).

### 1.2 Media (photo)

- DOCUMENTED (https://doc.wykop.pl/resources/media/photos/upload.yaml): allowed mime types `image/jpeg, image/jpg, image/pjpeg, image/gif, image/png, image/x-png`; **max 10 MB**; over-frequency uploads are rejected (HTTP 429 "Too many upload requests in short time"). So natively hosted media is images/GIFs only — no native video mime is documented.
- OBSERVED: entry images render with a **"Pobierz"** (download) control and a "źródło:" (source) caption linking to Wykop's CDN (`wykop.pl/cdn/...`); 18+ images/entries hide behind an interstitial (§6.2).

### 1.3 Embeds (external video/GIF/links)

- DOCUMENTED (https://doc.wykop.pl/components/schemas/embed.yaml): embed sources are **youtube, twitter, instagram, gfycat, streamable**; embeds carry a thumbnail, video metadata (title, duration…), a `commercial` flag, and an **age category** enum `all / age_12 / age_16 / age_18`.
- OBSERVED: YouTube-style embeds render as playable figures in streams (same embed classes as finding cards, sibling doc §2).
- Bare URLs in the body render as links; the entry schema also documents a `card` object — "internal embed occurring in the content" (link-preview card) (DOCUMENTED, https://doc.wykop.pl/components/schemas/entry.yaml).

### 1.4 Tags in the body

- Tags written in the body are live links into the shared tag namespace (§5.3). The API example content includes `#nsfw #wykop` inline. The tag character set/normalization behavior is the findings-side one (lowercase `[a-z0-9]+` observed; sibling doc §3).

### 1.5 Polls (ankiety)

- DOCUMENTED (https://doc.wykop.pl/components/schemas/create_update_survey.yaml and /resources/entries/entries_survey.yaml): a poll is created first (`POST /entries/survey`) and then attached to an entry by id. **Question: 5–100 chars, required. Answers: 2–10, each up to 50 chars.** The survey schema (https://doc.wykop.pl/components/schemas/survey.yaml) is marked **"Ankieta [tylko dla wpisu]"** — polls attach to entries only, never to comments; it exposes `count` (total votes) and per-answer counts.
- OBSERVED (https://wykop.pl/wpis/87290753/glowni-i-wulgarni-ludzie-wszedzie-psy-wariaci-drog): the poll renders as `section.survey result` — question as a heading ("Czy Polska to najgorszy kraj do życia w Europie?"), three answers each showing percentage and absolute count (e.g. 81.9% / 113), and the total line **"Oddanych głosów: 138"**. The body text sits above the poll. Anonymous visitors see full results and **no** voting control. More poll entries are easy to find via https://wykop.pl/tag/ankieta/wpisy.

### 1.6 Edit / delete

- **Edit window:** DOCUMENTED (API, https://doc.wykop.pl/resources/entries/entries_entry.yaml, PUT): only own entries; "Autor może modyfikować wpis przez 15 minut od daty dodania" — **15 minutes** from posting. The FAQ (https://wykop.pl/faq/tresci-dodawanie-glosowanie) states 15 min for findings and 10 min for comments but does **not** state the entry window; the API doc is the only public source of the 15-min entry figure.
- **Delete:** DOCUMENTED (same API resource, DELETE): "Autor może zawsze usunąć własny wpis" — the author can **always** delete their own entry, no time limit.
- OBSERVED: no edit/delete affordances render anonymously (they are actions of the logged-in owner); the entry schema documents `editable` and `deletable` flags per viewer. Deleted comments leave a visible placeholder in discussions (`entry reply deleted` class observed on /mikroblog).
- **Rate limits** (DOCUMENTED, https://wykop.pl/faq/konto, "Limity na koncie"): adding Mikroblog entries — green 15/30 min, orange 20/30 min, burgundy 30/30 min.

---

## 2. Mikroblog feeds

### 2.1 Section views (anonymous)

- `https://wykop.pl/mikroblog` — h1 "Mikroblog" with a filter dropdown whose server-rendered current value is **"gorące"** (hot) — hot is the default view (OBSERVED, DOM). This matches the API's sort enum: `[newest, active, hot]`, **default `hot`** (DOCUMENTED, https://doc.wykop.pl/components/parameters/entries.sort.yaml).
- **Hot time filters**: chips "2h / 6h / 12h / 24h" linking to `/mikroblog/gorace/{2,6,12,24}` (OBSERVED). The API's `last_update` parameter allows `[1, 2, 3, 6, 12, 24]` hours, default 12, and is "available only together with the hot filter" (DOCUMENTED, https://doc.wykop.pl/components/parameters/entries.filter.yaml). Which subset the web UI exposes vs the API is therefore 4 of 6 values.
- `/mikroblog/najnowsze` — newest, strictly reverse-chronological (OBSERVED).
- `/mikroblog/aktywne` — active (page title "Mikroblog (aktywne) :: Wykop.pl") (OBSERVED).
- The dropdown's full option list only hydrates client-side; the three views above were each confirmed by direct URL (OBSERVED). Logged-in-only feeds (observed tags/people, categories) are not reachable anonymously — the API's extra `category` and `bucket` parameters on `GET /entries` point at such personalization (see Open questions).
- **View toggles** on every Mikroblog view: **"Pełna lista"** (full list) / **"Tylko multimedia"** (only multimedia) (OBSERVED) — the API equivalent is a `multimedia=true` filter ("only objects with photos or embed", DOCUMENTED).
- **Pagination**: numbered pages `/mikroblog/strona/{n}`; 45–50 pages visible depending on view (OBSERVED). The API notes anonymous users page by number while logged-in users page by hash cursor, and that deep pagination ends with an explicit error (DOCUMENTED, https://doc.wykop.pl/resources/entries/entries.yaml).
- **No Mikroblog RSS**: the footer offers only the three findings feeds (/rss, /rss/upcoming, /rss/comments) (OBSERVED).

### 2.2 Stream anatomy (hot view, one server render, n=25 entries)

OBSERVED on https://wykop.pl/mikroblog:

1. Author: avatar + username colored by login tier, linking to `/ludzie/{username}`.
2. Plus score, rendered `+N` (class `plus`).
3. Relative timestamp (`title` holds the absolute datetime) linking to the permalink `/wpis/{id}/{slug}`.
4. Body (`section.entry-content`) with inline tags/mentions/media; truncated behind "Pokaż całość" when long; 18+ entries render the adult interstitial instead (§6.2).
5. A handful of recent replies inline under hot entries (48 `entry reply` blocks across the 25 entries), including author-marked replies (`entry reply author link-author`) and deleted placeholders; entries with more discussion show a **"Pokaż komentarze"** (show comments) expander (23 of 25 entries).
6. Actions: comment counter linking to the permalink, **"Odpowiedz"** (reply), **"Obserwuj dyskusję"** (follow discussion) per entry.

### 2.3 Hot promotion rule

- DOCUMENTED (https://wykop.pl/faq/tresci-dodawanie-glosowanie, "Kiedy mój Wpis pojawi się w Gorących?", paraphrased): an entry reaches Gorące when it draws large community interest — the more votes and the more commenter engagement, the higher the chance. No numeric threshold is published (contrast: findings' homepage promotion is a dig-count threshold within 24h, sibling doc §2).

### 2.4 Tag-driven views

- CONFIRMED (OBSERVED here and in sibling doc §4): one tag namespace serves both content types. `/tag/{name}` defaults to type "Wszystko" (everything) interleaving finding cards and entries (with nested replies as context); the type dropdown offers **"Znaleziska"** → `/tag/{name}/znaleziska` and **"Wpisy"** → `/tag/{name}/wpisy`; sort "Najnowsze"/"Najlepsze" (newest/best) switches client-side; a month archive lives at `/tag/{name}/archiwum/{YYYY-MM}`; pagination `/tag/{name}/strona/{n}`.
- OBSERVED (https://wykop.pl/tag/polska/wpisy): the entries-only stream renders entries exactly as in §2.2 (author, `+N`, body, inline tags, media with "Pobierz", reply count, "Odpowiedz", "Obserwuj dyskusję", "Pełna lista"/"Tylko multimedia" toggles), paginated to 500 pages on a large tag.
- Entry search exists as its own index at `https://wykop.pl/szukaj/wpisy?q=…` (page loads anonymously; result list hydrates client-side only, so result behavior was not verifiable from SSR; the API documents `GET /search/entries`).

---

## 3. Entry discussion (comments)

### 3.1 Structure: flat, one level

- OBSERVED (permalink https://wykop.pl/wpis/87290753/…): every comment is a sibling `entry reply` block under the entry — **no nesting, no reply-to-a-reply**. Replies address each other by the `@username:` convention at the start of the comment body (rendered as a profile link).
- DOCUMENTED: the comment schema's `parent` is the entry (https://doc.wykop.pl/components/schemas/entry_comment.yaml); comment endpoints are only `/entries/{entryId}/comments[/{commentId}]` — there is no deeper resource.
- Comments by the entry's author carry an author marker (`entry reply author link-author` class; rendered as an "Autor" badge) (OBSERVED).

### 3.2 What a comment can contain

- DOCUMENTED (https://doc.wykop.pl/components/schemas/create_update_comment.yaml): `content` (text; **may be empty when media is attached, otherwise min 5 chars**; only a `too_short` error is documented — no public max), one `photo` (same 10 MB image/GIF pipeline as entries), one `embed`, and an `adult` flag. **No survey** — polls are entry-only (§1.5).
- The comment content example includes `#nsfw #wykop` — tags can be written in comment bodies too (whether they index the comment on tag pages is not anonymously verifiable; see Open questions).
- OBSERVED: comments with images and blockquote-quoted text; "via Android / via iOS / via Wykop" device badges (the API documents the `device` field).

### 3.3 Ordering, counts, permalinks

- OBSERVED: comment list is strictly **chronological, oldest first**, with **no sort selector** — an asymmetry with finding comments, which offer najlepsze/najstarsze/najnowsze sorts (sibling doc §5). Long comment lists paginate on the permalink page ("Strona 1 z 9" observed on a 165-comment entry — Inference: ~20 per page).
- Each comment's timestamp links to a fragment permalink `/wpis/{entryId}/{slug}#{commentId}` (OBSERVED; numeric comment ids ~298 million).
- The entry shows a comment count in streams and "N odpowiedzi" (N replies) in sidebar widgets (OBSERVED).

### 3.4 Comment voting

- Plus-only, same mechanics as entries: `POST /entries/{entryId}/comments/{commentId}/votes` with no direction, `DELETE` to retract, 400 when the voter already voted **or is the comment's author**; the voter list is retrievable (DOCUMENTED, https://doc.wykop.pl/resources/entries/entries_comments_comment_votes.yaml). OBSERVED: every comment renders a `+` button (`button.plus`) with a numeric score.

### 3.5 Comment editing and author moderation

- Comment edit window: **10 minutes** (DOCUMENTED, https://wykop.pl/faq/tresci-dodawanie-glosowanie: editable "przez dziesięć minut" via an "edytuj" button).
- **Author moderation**: DOCUMENTED (same FAQ page, under the question about one's statements disappearing from another user's discussion): "Autorzy Wpisów mają możliwość moderowania własnych dyskusji na Mikroblogu" — entry authors may remove other users' comments under their own entries. This is a Mikroblog-specific power with no findings-side equivalent in the FAQ.
- Comment rate limits (generic "Komentarze"): 10/30/50 per 15 min by tier (DOCUMENTED, https://wykop.pl/faq/konto).
- OBSERVED: a deleted comment leaves a placeholder block in the discussion (`entry reply deleted`).

---

## 4. Entry voting

### 4.1 Plus-only — the suspected asymmetry is confirmed

- OBSERVED: everywhere an entry renders (Mikroblog streams, tag streams, permalinks, profiles, homepage), its vote UI is a single **"+"** button (`button.plus`) beside a score rendered `+N`. No minus, no bury (zakop) control, no downvote reason UI exists on any anonymous surface.
- DOCUMENTED (https://doc.wykop.pl/resources/entries/entries_entry_votes.yaml): the vote endpoint takes **no direction** — `POST /entries/{id}/votes` casts the (only) vote; `DELETE` retracts it ("Cofnięcie głosu"); voting twice or voting one's **own** entry returns an error ("Użytkownik głosował wcześniej na wpis lub jest jego autorem"). The action schema for entries names the capability `vote_up` (https://doc.wykop.pl/components/schemas/actions.entry.yaml).
- The site's own vocabulary is "plus": the profile subtab for entries a user voted on is **"Plusowane"** (OBSERVED, §5.4), and sidebar scores render `+N`.
- Contrast with findings: wykop/zakop (dig/bury) with bury reasons and a variable promotion threshold (sibling doc §2), and finding comments with +/- voting (sibling doc §5). Mikroblog has none of that machinery.
- **Schema caveat**: the entry *response* schema does declare `votes.up` and `votes.down` integers (https://doc.wykop.pl/components/schemas/entry.yaml) — Inference: schema reuse/legacy, since no down-vote endpoint or UI exists for entries; flagged in Open questions.
- Who-voted transparency: the API exposes the voter list of an entry publicly-shaped (`GET /entries/{id}/votes` returns user profiles); the web equivalent for anonymous visitors was not observed (hover cards are login-gated).

### 4.2 Vote weight

- Login colors "decide the strength and weight of a vote" (DOCUMENTED qualitatively, https://wykop.pl/faq/konto); no numbers are public, and how weight applies to plus-counts vs hot ranking is not stated.

---

## 5. Relation to findings surfaces

### 5.1 Navigation placement

- OBSERVED (header DOM of every fetched page): main nav order is **Główna | Wykopalisko (with live counter) | Hity | Mikroblog** — Mikroblog is a top-level sibling of the findings surfaces, not a sub-tab.

### 5.2 Feed separation and cross-promotion

- The homepage main stream is findings-first but **interleaves hot entries**: one server render of https://wykop.pl/ contained 25 finding cards (`link-block`) and **2 full entry blocks** (`entry stream-home`) inline in the stream — one of them an 18+ entry behind its interstitial (OBSERVED, n=1 render).
- Sidebar cross-promotion runs both directions (OBSERVED):
  - /wykopalisko sidebar: **"Gorące Wpisy — ostatnie 12h"** (hot entries of the last 12h: author, `+N`, timestamp permalink, reply count) (sibling doc §2, same date).
  - /mikroblog sidebar: **"Najnowsze Znaleziska"** (newest findings) and **"Aktywne Wpisy"** (active entries, with "Pokaż więcej" → /mikroblog/aktywne) plus "Popularne tagi".
- The Hits section (`/hity/…`) server-renders findings only (0 `/wpis/` links on /hity/tygodnia) — yet the API documents `GET /hits/entries` with year/month parameters (DOCUMENTED, https://doc.wykop.pl/resources/hits/entries.yaml). Where hit *entries* surface in the web UI is an open question.
- Search keeps separate indexes: `/szukaj/znaleziska` vs `/szukaj/wpisy` (OBSERVED URLs).

### 5.3 Shared tag namespace

- Confirmed: one tag page per name serves both content types with the Wszystko/Znaleziska/Wpisy type filter (§2.4); the default "Wszystko" stream mixes finding cards and entries in one list (OBSERVED here and sibling doc §4). Microblog-dominated tags exist (liganauki: 3 findings vs 17 entries on page 1, sibling doc).

### 5.4 User profiles

- OBSERVED (https://wykop.pl/ludzie/codziennaKasia…): profile tabs are **"Akcje"** (default, combined activity), **"Znaleziska"** → `/ludzie/{u}/znaleziska/dodane`, **"Mikroblog"** → `/ludzie/{u}/wpisy/dodane`, "Obserwujący", "Obserwowane" — each with counts. The Mikroblog tab has three subtabs: **"Dodane"** (added) `/wpisy/dodane`, **"Komentowane"** (commented) `/wpisy/komentowane`, **"Plusowane"** (plussed) `/wpisy/plusowane` — mirroring the API's added/commented/voted profile endpoints (DOCUMENTED, doc.wykop.pl index).
- Entries render on profiles as full stream blocks (score, media, replies, "Pokaż całość") (OBSERVED). A **"Mikroblogger"** achievement badge exists on profiles (OBSERVED), i.e. entry activity feeds the achievements system.

---

## 6. Other observable behavior

### 6.1 Permalinks and lifecycle

- Entry permalink: `/wpis/{numericId}/{slug}`; the slug derives from the content and falls back to the literal `wpis` for media-only entries (OBSERVED, e.g. /wpis/87328779/wpis). Comment anchor: `#{commentId}` (§3.3). Old `www.wykop.pl` URLs redirect to apex; a bare `/wpis/{id}` canonicalizes to the slugged URL; missing ids give the standard 404 ("Strony nie znaleziono") (OBSERVED).
- Old entries stay publicly readable: a 2017 entry renders fully today, with its timestamp shown **absolute** ("16.08.2017, 12:52:38") where fresh entries show relative time (OBSERVED). The API documents an `archive` boolean on entries ("czy wpis pochodzi z archiwum"); no archive banner or disabled state is visible anonymously — what archiving changes is an open question.

### 6.2 18+ / NSFW handling

- OBSERVED (streams, homepage, sidebars): an adult-flagged entry renders an `adult-ribbon` (tooltip "Treść przeznaczona dla osób powyżej 18 roku życia") and replaces the body with the interstitial "Treść przeznaczona dla osób powyżej 18 roku życia…" + **"Pokaż treść"** (show content) button.
- DOCUMENTED: `adult` boolean exists on entries **and** comments (create schemas); embeds carry age categories all/12/16/18; per /dobre-praktyki some content is hidden from users who are not registered adults (sibling doc §1). The `#nsfw` tag appears in the API's own content examples; the tag-vs-flag relationship is an open question.

### 6.3 Counts shown

- Entry: plus score (`+N`) and comment count (stream counter; "N odpowiedzi" in widgets). Comment: its own plus score. Poll: total votes ("Oddanych głosów: N") and per-answer counts + percentages. (All OBSERVED.)
- No view counts, share counts, or vote-breakdowns render anywhere anonymously.

### 6.4 Misc

- "via" device badges on entries/comments (Android/iOS/Wykop) — the documented `device` field (OBSERVED + API).
- Favourites ("ulubione") and pinning an entry to one's author tag exist as per-viewer capabilities in the entry schema (`favourite`, `pinnable` — "czy użytkownik może przypiąć wpis do tagu autorskiego") (DOCUMENTED); neither renders anonymously.
- "Obserwuj dyskusję" carries the notification tooltip ("Otrzymuj powiadomienia o nowych komentarzach") (OBSERVED on entry pages, matching findings behavior).

---

## Open questions (login-gated or unobservable)

Could **not** be verified anonymously; input for the ticket #55 decision session:

1. **The entry composer**: field layout, formatting toolbar (which of bold/underline/link/spoiler/quote/code get buttons), how photo vs embed vs poll attachment is chosen, tag/mention autocomplete, where the adult toggle sits, client-side length counter for the 5–20 000 char range.
2. **Entry edit window on the web**: the API says 15 minutes — does the web UI match, and are there extra conditions (e.g. does editing survive receiving plusses/comments)? The FAQ is silent for entries.
3. **votes.down on entries**: the response schema declares a down count with no endpoint or UI — dead schema or a real hidden behavior (e.g. moderator-only)?
4. **Who-plussed list in the web UI**: the API exposes voter lists for entries and comments; is there a hover/click surface showing "kto plusował" when logged in?
5. **Poll voting UX**: single vs multi-choice (schema suggests single), whether a vote can be changed, whether the entry author can vote in their own poll, poll lifetime/closing, live result updates.
6. **Personalized feeds**: what the logged-in Mikroblog dropdown offers beyond gorące/najnowsze/aktywne (observed-tags feed? "Moje"? categories/buckets per the API `category`/`bucket` params), and how hash-based pagination changes browsing.
7. **Hot algorithm**: the engagement factors are documented only qualitatively — no threshold, decay, or vote-weight math is public; also whether the 2h/6h/12h/24h chips map to the API's extra 1h/3h values anywhere.
8. **Scope of author moderation**: does "moderowanie własnych dyskusji" mean deleting comments only, or also blocking users from the discussion? Any notification/trace for the removed commenter? (A removed comment leaves a visible "deleted" placeholder — is that the same artifact?)
9. **Comment tags**: do tags written in a comment index that comment on tag pages (tag streams show replies only as context under matching entries), and do comment @-mentions notify like entry mentions?
10. **Comment max length**: only `too_short` is documented; the maximum (if any) is unknown.
11. **Spoiler syntax details**: the `!` line prefix is confirmed; exact multiline/nesting rules and whether a toolbar inserts it are unknown; ditto any code-block syntax.
12. **Native video/GIF hosting**: photo uploads document images/GIF only — has newer native video (beyond the five embed providers) been added for logged-in users?
13. **Hit entries surface**: where does the web UI show `GET /hits/entries` content (the Hits pages server-render findings only)? Is there a "Wpisy" toggle on /hity for logged-in users?
14. **Archive semantics**: what changes when `archive=true` (commenting closed? voting closed?) — old entries render normally anonymously.
15. **18+ mechanics on submission**: how the adult flag is set (checkbox? auto via #nsfw tag? media age rating), and the relationship between the `#nsfw` tag and the `adult` flag.
16. **Following an entry's discussion**: exact notification behavior of "Obserwuj dyskusję" and the observed-discussions notification feed (API documents the endpoints).
17. **Deleting an entry with discussion**: the author "can always delete" — what happens to the comments under it?
18. **Anonymous pagination caps**: streams stop at ~50 pages (45–50 observed) and the API errors on deep pagination — the exact cap and whether login lifts it are unknown.

---

## Source URLs (all loaded 2026-08-18, live — no archived captures needed)

**wykop.pl pages (server-rendered HTML):**

- https://wykop.pl/mikroblog — default hot view, h1 + "gorące" filter value, 2h/6h/12h/24h chips, stream anatomy (25 entries), inline replies, "Pokaż komentarze", sidebar widgets ("Najnowsze Znaleziska", "Aktywne Wpisy", "Popularne tagi"), footer RSS
- https://wykop.pl/mikroblog/najnowsze, https://wykop.pl/mikroblog/aktywne, https://wykop.pl/mikroblog/gorace — view sub-URLs and titles
- https://wykop.pl/wpis/87290753/glowni-i-wulgarni-ludzie-wszedzie-psy-wariaci-drog — poll rendering, flat comments, chronological order, comment anchors, plus buttons, mentions, blockquotes
- https://wykop.pl/wpis/87320815/i-to-by-bylo-na-tyle-jesli-chodzi-o-poyebane-upaly — entry + 165-comment pagination ("Strona 1 z 9"), comment anatomy
- https://wykop.pl/wpis/26122617/pokaz-spoiler-got — spoiler rendering ("Pokaż spoiler", `!` prefix via og:title), 2017 entry longevity, absolute timestamps
- https://wykop.pl/wpis/1000000, https://wykop.pl/wpis/30000000 — 404 behavior, `/wpis/{id}/wpis` slug fallback
- https://wykop.pl/ — main nav order, 25 finding cards + 2 inline `entry stream-home` blocks, sidebar h4s
- https://wykop.pl/tag/polska/wpisy, https://wykop.pl/tag/famemma — entries-only tag view, toggles, 500-page pagination
- https://wykop.pl/hity/tygodnia — Hits page: findings-only SSR, period/archive dropdowns
- https://wykop.pl/ludzie/codziennaKasia and /wpisy/dodane — profile tabs, Mikroblog subtabs (Dodane/Komentowane/Plusowane), badges
- https://wykop.pl/szukaj/wpisy?q=spoiler — entries search index exists; results hydrate client-side

**Wykop FAQ (documented behavior):**

- https://wykop.pl/faq — category index
- https://wykop.pl/faq/definicje — Mikroblog and Hashtagi definitions
- https://wykop.pl/faq/tresci-dodawanie-glosowanie — hot criteria for entries, 15-min finding / 10-min comment edit windows (entry window absent), author moderation of own Mikroblog discussions (verbatim quoted in §3.5)
- https://wykop.pl/faq/konto — per-color limits: entries 15/20/30 per 30 min, comments 10/30/50 per 15 min, mentions 20/50/150; vote weight qualitative
- https://wykop.pl/faq/moderacja, https://wykop.pl/faq/podstawy — checked; no additional Mikroblog specifics

**Wykop API v3 documentation (official, doc.wykop.pl; "Wykop API v3 (beta)", Swagger index https://doc.wykop.pl/openapi.yaml):**

- /resources/entries/entries.yaml — list/create entries; anonymous vs logged-in pagination; pagination cap error
- /resources/entries/entries_entry.yaml — 15-min edit, delete-anytime
- /resources/entries/entries_entry_votes.yaml — plus-only vote, retract, self-vote ban, voter list
- /resources/entries/entries_comments.yaml, entries_comments_comment_votes.yaml — flat comments, plus-only comment votes
- /resources/entries/entries_survey.yaml — poll creation flow
- /resources/media/photos/upload.yaml — image mime types, 10 MB cap, upload areas
- /resources/hits/entries.yaml — hit entries endpoint
- /components/parameters/entries.sort.yaml (newest/active/hot, default hot), entries.filter.yaml (last_update 1–24h), multimedia.yaml
- /components/schemas/create_update_entry.yaml (5–20 000 chars, one photo/embed/survey, adult), create_update_comment.yaml (empty-with-media, min 5), create_update_survey.yaml (question 5–100, answers 2–10×50), survey.yaml ("tylko dla wpisu"), entry.yaml (votes.up/down, favourite, pinnable, archive, device, card), embed.yaml (5 providers, age categories), photo.yaml, actions.entry.yaml (vote_up)

**Cross-reference:** `docs/research/wykop-finding-submission-and-tags.md` (same-day sibling research) for the findings-side facts cited in §2.4, §4.1, §5.2, §5.3 and the consent-dialog description.
