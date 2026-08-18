# Wykop.pl: Finding ("znalezisko") submission and tags — observed behavior

- **Research date:** 2026-08-18
- **Method / constraint:** anonymous (logged-out) access only, via live wykop.pl pages and the official FAQ/help pages on wykop.pl. No login, no account, no submissions attempted. Where behavior is login-gated, that fact is recorded instead. No secondary sources were used for claims.
- **Consent handling:** wykop.pl shows a TCF consent dialog ("Dbamy o Twoją prywatność", CMP by tri-table, "1015 partnerów") with buttons "Dostosuj wybory" (customize choices) and "Zaakceptuj wszystko" (accept all). No consent was granted during this research; all observations were made without accepting anything. A footer control "Ustawienia prywatności" (privacy settings) reopens the dialog.
- **Rendering note:** wykop.pl is a Vue SPA with server-side rendering. List items ("link-block" cards) hydrate lazily in the browser; observations below come from the live DOM and from the server-rendered HTML of the same URLs fetched in-session. Screenshots were not available in this session (headless browser pane), so all layout facts are from the accessibility tree and DOM structure, not pixels.
- Polish UI labels are quoted as observed, with English translations in parentheses. Help-center prose is paraphrased, not copied.

---

## 1. Submission flow (as observable anonymously)

### Entry point and login gate

- The global header of every page has a "Dodaj" (Add) link pointing to `/dodaj-link`. (Source: https://wykop.pl/ header nav, observed 2026-08-18.)
- For an anonymous visitor, `https://wykop.pl/dodaj-link` redirects to the login page: final URL `https://wykop.pl/logowanie?redirect=%2Fdodaj-link` (page title "Logowanie :: Wykop.pl"). **The submission form is fully login-gated; none of it is visible anonymously.**
- The legacy URL `https://wykop.pl/dodaj` also redirects to `/logowanie?redirect=%2Fdodaj-link` — i.e. it is an alias of `/dodaj-link`.
- The login page offers tabs "LOGOWANIE" (login) / "REJESTRACJA" (registration), SSO buttons "Zaloguj się z Facebook" / "Zaloguj się z Apple" (the sidebar login widget also shows "Zaloguj się z Google"), plus "Login" / "Hasło" (password) fields and "nie pamiętam hasła" (forgot password). (Source: https://wykop.pl/logowanie.)
- The old help subdomain `https://pomoc.wykop.pl` no longer exists: it 301-redirects to `https://wykop.pl/`. Help now lives at `https://wykop.pl/faq` (with category subpages), `https://wykop.pl/dobre-praktyki` (good practices) and `https://wykop.pl/standardy-moderacji` (moderation standards).

### What a finding is made of (official definition)

Paraphrased from https://wykop.pl/faq/definicje, section "Znalezisko":

- A finding consists of: a **URL address**, a **title**, a **short description**, and sometimes a **video player** and/or a **graphic called the thumbnail** ("miniatura").
- The same page defines "Linki powiązane" (related links): supplementary links that extend a finding's topic; **any interested user can add related links to an existing finding** (not just the author).

The FAQ page https://wykop.pl/faq/tresci-dodawanie-glosowanie adds (paraphrased):

- Allowed content: links to articles, videos, blogs, other online statements; users can also create their own articles/entries, comment, and vote. Adding links to one's own sites/blogs is allowed but "niemile widziane" (frowned upon), especially if ad-heavy.
- **Editing window: 15 minutes** from adding a finding, covering title, description, graphic, and tags, via an "edytuj" (edit) button on the finding's detail page. After that, changes require contacting the site via the contact form. (Comments have a separate 10-minute edit window.)

### Validation rules and length limits

- **No numeric length limits for title/description are documented anywhere publicly observable.** The FAQ describes the fields qualitatively only. (Checked: /faq, /faq/definicje, /faq/tresci-dodawanie-glosowanie, /dobre-praktyki.)
- The only numeric input constraint observed anywhere anonymously: the global header search box has `maxlength="50"` (source: DOM of https://wykop.pl/tag/polska header).

### Duplicate-URL handling

From https://wykop.pl/dobre-praktyki, item "Nie duplikuj" (paraphrased):

- Users are told to always check whether a finding already exists before adding it, and that a **"wyszukiwarka duplikatów" (duplicate search tool)** helps with this — i.e. duplicate detection exists in the submission flow.
- Changing the submitted link to evade duplicate detection is explicitly called cheating ("Nie próbuj oszukiwać zmieniając link Znaleziska"), implying the duplicate check blocks resubmission of a URL that was already posted.
- The exact anonymous-visible counterpart: the public search engine can filter findings by source domain (`/szukaj/znaleziska?domains=youtube.com` — observed working) — but the login-side duplicate-check UX itself is not observable (see Open questions).

### Rate limits and moderation on submission (official)

From https://wykop.pl/faq/konto, "Limity na koncie" (account limits) — limits depend on the account's login color (seniority/activity tier):

| Action | "Zieloni" (green, <30 days) | "Pomarańczowi" (orange, ≥30 days) | "Bordowi" (burgundy, >90 days + high activity) |
|---|---|---|---|
| Adding findings ("Dodawanie Znalezisk") * | 6 / 12h | 12 / 6h | 96 / 24h |
| Comments | 10 / 15 min | 30 / 15 min | 50 / 15 min |
| Mikroblog entries | 15 / 30 min | 20 / 30 min | 30 / 30 min |
| Mentions ("Przywoływanie") | 20 people | 50 people | 150 people |
| Private messages | 10 conversations / 1h | 50 / 1h | 100 / 1h |

\* Footnote on the same page: **for findings from the same domain, the limit is 50% lower.**

- Login colors (same page): green = account younger than 30 days; orange = after 30 days; burgundy ("bordowy") = over 90 days and notably active; black = Wykop staff. Colors affect "siła i waga głosu" (vote strength/weight), ad volume, and fast-reporting ability.
- Moderation (https://wykop.pl/faq/moderacja, paraphrased): content violating the terms or the moderation standards is removed based on user reports ("zgłoś" (report) option is available on comments, findings, and user profiles; also via contact form). Removals can lead to account bans; bans block adding/voting/commenting but not account access. Appeals go through the contact form.
- https://wykop.pl/dobre-praktyki also states (paraphrased): vote manipulation (digging one finding from multiple accounts) is banned; deliberately mismatched bury reasons ("powód zakopu") are frowned upon; unarranged advertising campaigns end in account blocks; embedded media must be age-rated per the terms' attachment no. 2, and **some content is hidden from users who are not registered and have not declared adulthood** (this matches the observed 18+ interstitial, section 2).

---

## 2. Where a fresh finding lands: Wykopalisko

### Surface and lifecycle

- Official definition (paraphrased from https://wykop.pl/faq/definicje, "Wykopalisko"): the place where users' newly added findings appear. **Every added link goes to Wykopalisko for 24 hours**; during that time users' votes decide whether it moves to the homepage ("Strona główna") or to the archive ("Archiwum").
- Promotion rule (paraphrased from https://wykop.pl/faq/tresci-dodawanie-glosowanie, "Kiedy moje Znalezisko trafi na Stronę główną?"): a finding is promoted if it reaches the required number of digs ("wykopy") within 24 hours; the threshold is **not constant** — it depends on the weight of individual votes, the number of buries ("zakopy"), and the bury reasons chosen by voters.
- URL: `https://wykop.pl/wykopalisko`. The header nav item "Wykopalisko" carries a live counter badge (`upcoming-count`, showing "199" during research) — the number of current upcoming findings. (Source: header DOM on https://wykop.pl/tag/polska.)
- RSS feeds exist: `/rss` (labelled "Wykopane" — dug/promoted), `/rss/upcoming` (labelled "Wykopalisko"), `/rss/comments` ("Komentowane"). (Source: footer of https://wykop.pl/wykopalisko.)

### Sections / sorts

- Wykopalisko has a sort dropdown with four options (observed by opening the dropdown on https://wykop.pl/wykopalisko): **"najnowsze"** (newest) → `/wykopalisko/najnowsze`, **"aktywne"** (active; the default — page loads with this label) → `/wykopalisko/aktywne`, **"wykopywane"** (being dug up) → `/wykopalisko/wykopywane`, **"komentowane"** (commented) → `/wykopalisko/komentowane`.
- Pagination: numbered pages at `/wykopalisko/strona/{n}` (8 pages shown at research time).
- The homepage stream ("Główna", https://wykop.pl/) has its own sort dropdown with two options: "najnowsze" → `/najnowsze` and "aktywne" → `/aktywne`. The homepage also shows a "Hity Wykopu" (Wykop hits) carousel linking to `/hity/dnia` / `/hity/tygodnia`.

### Finding card anatomy (upcoming stream)

Extracted from the server-rendered DOM of https://wykop.pl/wykopalisko (card = `section.link-block stream-upcoming`, id `link-{numericId}`). Structure in order:

1. **Vote box** (`section.vote-box`): dig count (e.g. "40") + button labelled **"Wykop"** (dig). No bury ("zakop") control is rendered for anonymous visitors anywhere on the card.
2. **Title**: `h2 > a` linking to the internal detail page `/link/{id}/{slug}`.
3. **Thumbnail** (`figure`): for video sources the figure carries an embed class (e.g. `embed youtube`) with a play-icon overlay; 18+ findings render an `adult-ribbon` and an `adult-filter` overlay with the label **"Pokaż treści 18+"** (show 18+ content) instead of the image.
4. **Description excerpt** (first sentences of the finding's description).
5. **Byline row**: literal "z" (from) + **author username** linking to `/ludzie/{username}` (username colored by tier, e.g. class `burgundy-profile`) + **source domain** as a link (`a.external`) — but pointing to the internal domain search `/szukaj/znaleziska?domains={domain}`, not to the source; + relative timestamp `time.date`, format **"dodany: 1 godz. i 34 min temu"** (added: 1 h 20 min ago).
6. **Actions row** (`section.actions`): comment counter linking to the detail page, then the **tag list** — each tag as `li.tag` rendered as "#" + tag name linking to `/tag/{name}`.
7. Hover-card scaffolding (`popper ... guest` classes) exists around author, domain, and each tag — the logged-out variant of hover tooltips.

Observed across the 25 upcoming cards present in one server render of `/wykopalisko`: **every card had between 2 and 6 tags** (distribution: seventeen cards with 6, three with 5, two with 4, one with 3, one with 2; none with 0, 1, or more than 6).

Sidebar of `/wykopalisko` (right rail): "Hity" (dnia/tygodnia) top-findings widget (title + dig count), "Gorące Wpisy — ostatnie 12h" (hot microblog entries of last 12h: author, +score, timestamp permalink `/wpis/{id}/…`, reply count), "Wykopalisko — najnowsze" (latest five upcoming titles), and "Popularne tagi" (popular tags cloud linking to `/tag/{name}`).

---

## 3. Tag syntax and conventions

### Official definition

Paraphrased from https://wykop.pl/faq/definicje, section "Hashtagi":

- Tags are **one-word** ("jednowyrazowe") words categorizing threads; a hashtag is the hash sign (#) plus a chosen word, e.g. `#ciekawostki`.
- Users can follow tags, co-create them with others, and found their own tags — **"tagi autorskie" (author tags)** — to build communities around them. This is the only regular-vs-special tag distinction documented; no "official"/verified tag badge was observed anywhere on tag pages.
- https://wykop.pl/faq/tresci-dodawanie-glosowanie states that tag pages always display **all** activities marked with the tag (users are told to check the "wszystkie" (all) filter if they miss their own content).

### Observed character set and case

- All **84 unique tag names** collected from the 25 server-rendered `/wykopalisko` cards match `^[a-z0-9]+$` — lowercase ASCII letters and digits only. No uppercase, no Polish diacritics, no hyphens, no underscores, no other punctuation. Examples: `wszechswiat` (not `wszechświat`), `gruparatowaniapoziomu`, `f1`, `4konserwy`, `2137` (digits-only tag observed on a microblog entry under /tag/polska).
- **Case handling:** `https://wykop.pl/tag/POLSKA` normalizes to `https://wykop.pl/tag/polska` (final URL, canonical link, and `h1` all lowercase) — tag URLs are case-insensitive with a lowercase canonical form.
- URL probes: `/tag/wszechświat`, `/tag/rozowe_paski`, `/tag/rozowe-paski` all return **404 "Nie ma takiej strony (Błąd 404)"**. Control: a nonsense-but-ASCII tag (`/tag/qwertyzxcvbnm1234567`) also 404s, so a 404 only proves the tag does not exist, not that its spelling is illegal. No tag containing diacritics, hyphen, or underscore was observed anywhere.
- **Search autocomplete folds diacritics and strips punctuation** (observed on https://wykop.pl/szukaj/wszystkie, "Tagi" filter, working anonymously): typing `świat` returns `#swiat`, `#swiatnauki`, …; typing `rozowe_` returns exactly the same suggestions as `rozowe` (`#rozowepaski`, `#rozowebrudaski`, …). Every suggestion shows the tag name plus an observer count, e.g. "#rozowepaski 3091 obserwujących" (observers), "#swiat 29554 obserwujących".

### Count limits per finding

- Not documented publicly. Observed on findings: **2–6 tags per finding** (see section 2; n=25). Observed on a Mikroblog entry: **11 tags** on a single entry under /tag/polska — so whatever cap findings have does not apply (or is not enforced the same way) on Mikroblog entries.

---

## 4. Tag page anatomy

Two tag pages inspected: **https://wykop.pl/tag/polska** (large) and **https://wykop.pl/tag/liganauki** (small). Both have identical structure.

### Header

- `h1` = bare tag name without "#" ("polska"; "liganauki"); page title = "#polska :: Wykop.pl".
- **"Obserwuj"** (observe/follow) button with tooltip title "Dodaj do obserwowanych" (add to observed) and an **observer count on the button**: "148k" for polska, "2.6k" for liganauki. For anonymous visitors the button is an anchor to `/logowanie`. Empty placeholder slots next to it suggest more controls render for logged-in users (unverified).
- **No tag description, no related-tags block, no owner/moderator info** is shown on either page anonymously.

### Dropdown filters (three, observed by opening each on /tag/polska)

1. **Type** (`data-dropdown="type"`), default **"Wszystko"** (everything): options "Wszystko" → `/tag/polska`, **"Znaleziska"** (findings) → `/tag/polska/znaleziska`, **"Wpisy"** (microblog entries) → `/tag/polska/wpisy`. So findings and Mikroblog entries share one tag namespace and one combined default view, with dedicated per-type sub-URLs instead of tabs.
2. **Sort** (`data-dropdown="sort"`), default **"Najnowsze"** (newest): options "Najnowsze" and "Najlepsze" (best) — rendered as client-side switches without dedicated URLs.
3. **Archive** (`data-dropdown="archive"`), labelled **"Archiwum"**: a month picker with prev/next year, each month linking to `/tag/polska/archiwum/{YYYY-MM}`.

### Stream content

- The default ("Wszystko", "Najnowsze") stream **interleaves finding cards and microblog entries**: on /tag/polska page 1, 14 `link-block stream-tag` cards + 6 `entry stream-tag` microblog entries + 7 `entry reply` nested replies; on /tag/liganauki, 3 findings + 17 entries + 34 replies (microblog-dominated tag).
- A microblog entry in the tag stream shows: author (avatar + username → `/ludzie/{user}`), relative timestamp linking to the entry permalink `/wpis/{id}/{slug}`, a vote score with "+" control, body text, its tag list (hashtags inline after the body; 11 tags observed on one entry), and for image attachments a "źródło:" (source:) caption link. View-toggle buttons "Pełna lista" (full list) / "Tylko multimedia" (only multimedia) and per-entry "Pokaż całość" (show all) / "Obserwuj dyskusję" (follow discussion) controls appear in the stream.
- Pagination: `/tag/{name}/strona/{n}`; both inspected tag pages showed page links up to 500.
- Unknown tags 404 (see section 3) — there is no empty-state tag page anonymously.

---

## 5. Tags on cards and on the finding detail page

### On cards (feed/tag streams)

- Tags sit in the card's bottom **actions row**, after the comment counter: `ul > li.tag`, each rendered as a "#" prefix span + tag-name anchor to `/tag/{name}` (see section 2 for the full card anatomy). Same presentation in the Wykopalisko stream and in tag-page finding cards.
- Each tag anchor is wrapped in tooltip scaffolding (`tag-actions guest` popper for anonymous visitors), implying per-tag hover actions for logged-in users (unverified).

### On the finding detail page

Inspected: https://wykop.pl/link/7997791/jak-geometria-pomaga-nam-zrozumiec-wszechswiat-prof-maciej-dunajski and https://wykop.pl/link/7997795/stara-bialka-moze-zostac-zalana.

Layout order observed (`section.link-block detailed`):

1. Vote box with dig count + "Wykop" button (class `hot` variant observed on a high-dig finding; no bury control anonymously).
2. `h1.heading` title — an anchor with `target="_blank"` but **no `href` in the DOM** (server-rendered or hydrated): the outbound source URL is not exposed in the page source anonymously; only the **source domain name** is displayed (e.g. "youtube.com"), linking internally to `/szukaj/znaleziska?domains=youtube.com`.
3. Media: YouTube findings render an embedded player (`figure` with embed classes; card variant class `no-thumbnail` on the detail block); image findings render the image with a "źródło:" caption linking to Wykop's CDN copy (`wykop.pl/cdn/...`).
4. Description (full), then byline: author (avatar + colored username → `/ludzie/{user}`), "z {domain}", "dodany: {relative time}".
5. **Tag row** — identical `li.tag` list as on cards ("#" + anchor per tag; the 6 tags of the sample finding: ciekawostki, gruparatowaniapoziomu, nauka, matematyka, qualitycontent, liganauki), placed in `section.actions detailed` **after the byline and before the comment bar**.
6. Comment bar: comment counter, "Odpowiedz" (reply), a share dropdown, and "Obserwuj dyskusję" (follow discussion) button with tooltip "Otrzymuj powiadomienia o nowych komentarzach" (receive notifications about new comments).
7. Comments stream with its own sort dropdown, default **"najlepsze"** (best); options "najstarsze" (oldest) → `/link/{id}/{slug}/najstarsze`, "najnowsze" (newest) → `/link/{id}/{slug}/najnowsze`, "najlepsze" → `/link/{id}/{slug}/najlepsze`. Comments have +/- vote buttons and per-comment "Obserwuj dyskusję" toggles.
8. No "Linki powiązane" (related links) block was present on the sampled detail pages (the feature is documented in /faq/definicje; presumably it renders only when such links exist — unverified).

---

## 6. Open questions (login-gated; to fill from the project owner's knowledge)

Everything below could **not** be verified anonymously and must not be treated as fact until answered:

1. **The submission form itself** (`/dodaj-link`): field list and order, which fields are required, exact validation messages, title/description length limits (no numeric limits are documented publicly), thumbnail selection UX (auto-scraped candidates? upload?), how video/embed sources are detected, whether a finding can be text-only/self (no external URL).
2. **Duplicate-URL UX at submission**: the FAQ confirms a duplicate checker exists ("wyszukiwarka duplikatów", /dobre-praktyki), but not whether it hard-blocks vs warns, how it canonicalizes URLs (query strings, http/https, trailing slash), or whether it links to the existing finding.
3. **Tag input mechanics in the form**: autocomplete presence/behavior, whether "#" is typed or implied, enforced minimum/maximum tag count for a finding (observed range 2–6; whether both ends are hard limits is unknown), and whether the same limit differs on Mikroblog (11 tags observed on an entry).
4. **Formal tag charset rule**: everything observed is lowercase `[a-z0-9]+`; whether uppercase/diacritics/punctuation are rejected with an error or silently normalized at input is unknown (search folds diacritics and strips "_", which suggests normalization somewhere, but the form behavior is unverified).
5. **Bury ("zakop") mechanics**: no bury control renders anonymously anywhere; the bury-reasons list ("powody zakopu", mentioned in /faq and /dobre-praktyki), where the button appears, and whether bury counts are displayed are unknown.
6. **Promotion algorithm specifics**: the FAQ documents 24h window + variable threshold from vote weights, bury counts, and bury reasons — the actual formula/thresholds are not public.
7. **"Tagi autorskie" (author tags)**: how founding a tag works, what ownership/moderation rights the founder has, and how an author tag is distinguished in UI.
8. **Tag observation and blocking**: what "Obserwuj" does when logged in (notifications? feed inclusion?), whether tags can be black-listed, and what the hidden header controls next to "Obserwuj" are.
9. **Per-tag hover actions**: the `tag-actions` tooltip scaffolding for logged-in users.
10. **18+ flow on submission**: how a finding is marked adult (checkbox? auto-detection?), given anonymous users see "Pokaż treści 18+" interstitials and /dobre-praktyki mentions age-rating duties.
11. **"Linki powiązane"**: the add/list UI on detail pages (documented concept; not rendered on the sampled pages).
12. **Vote weight values** per login color (documented qualitatively only).
13. **"Kategorie"** (user-defined bundles of tags, users, and phrases, /faq/definicje) — entirely login-side.
14. **Exact form validation messages** for every rule above — all login-side.

---

## Source URLs (all loaded during this research, 2026-08-18)

- https://wykop.pl/ — homepage, header/footer nav, sort dropdown, Hity carousel
- https://wykop.pl/dodaj-link and https://wykop.pl/dodaj — both → `/logowanie?redirect=%2Fdodaj-link`
- https://wykop.pl/logowanie — login page contents
- https://pomoc.wykop.pl — 301 → https://wykop.pl/
- https://wykop.pl/wykopalisko (+ SSR fetch of same) — surface, sorts, pagination, card anatomy, tag counts, adult filter, sidebar, RSS links
- https://wykop.pl/link/7997791/jak-geometria-pomaga-nam-zrozumiec-wszechswiat-prof-maciej-dunajski — detail page anatomy, comment sorts
- https://wykop.pl/link/7997795/stara-bialka-moze-zostac-zalana — detail page (image variant), hidden outbound href
- https://wykop.pl/tag/polska, https://wykop.pl/tag/POLSKA, https://wykop.pl/tag/polska/znaleziska — large tag page, case normalization, type/sort/archive dropdowns
- https://wykop.pl/tag/liganauki — small tag page
- https://wykop.pl/tag/wszechświat, /tag/rozowe_paski, /tag/rozowe-paski, /tag/qwertyzxcvbnm1234567 — 404 probes
- https://wykop.pl/szukaj → /szukaj/wszystkie and https://wykop.pl/szukaj/znaleziska?domains=youtube.com — search filters, anonymous tag autocomplete with observer counts
- https://wykop.pl/faq — help index; categories
- https://wykop.pl/faq/definicje — definitions: Strona główna, Wykopalisko (24h), Znalezisko (URL+title+description+thumbnail), Mikroblog, Kategorie, Hashtagi (one-word; author tags), Linki powiązane
- https://wykop.pl/faq/tresci-dodawanie-glosowanie — content types, 15-min finding edit window, tag "wszystkie" filter note, promotion rule (24h, vote weight, bury reasons)
- https://wykop.pl/faq/konto — login colors, per-color rate-limit table (findings 6/12h, 12/6h, 96/24h; same-domain −50%), bans
- https://wykop.pl/faq/moderacja — reporting ("zgłoś" on findings/comments/profiles), removal policy, appeals
- https://wykop.pl/dobre-praktyki — duplicate checker, anti-evasion, vote manipulation, bury-reason misuse, 18+ marking and anonymous hiding, self-promotion rules
- https://wykop.pl/standardy-moderacji — report-reason taxonomy (flood/irritation, CSAM, spam account, profile violations, …)
