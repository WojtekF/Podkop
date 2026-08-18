# Podkop

A Wykop-style social link-aggregation platform: users submit findings, vote on them, and the best ones get promoted to the main page.

## Language

### Content & Feeds

**Finding**:
A link (or content) submitted by a user for the community to vote on. The central content unit of the platform.
_Avoid_: Post, sink item

**Feed**:
An ordered, pageable listing of content selected by a rule. The findings feeds are the Main Page (promoted findings) and Upcoming (fresh findings); the Microblog has its own newest-first feed; each signed-in user has an Observed Feed.
_Avoid_: Stream, timeline, list

**Main Page**:
The feed showing only Promoted findings. This is the site's front page.
_Avoid_: Home feed, all-posts feed

**Upcoming**:
The feed showing all fresh, not-yet-promoted findings (Wykop's *Wykopalisko*).
_Avoid_: New, pending

**Promotion**:
The one-way transition of a finding from Upcoming to the Main Page.
_Avoid_: Featuring, trending

**Source**:
The external URL a finding points to.
_Avoid_: Link, target

**Microblog**:
The top-level section where users publish Entries, alongside the findings surfaces and sharing the one tag namespace (Wykop's *Mikroblog*).
_Avoid_: Blog, board, forum

**Entry**:
A free-form text publication on the Microblog (Wykop's *wpis*): a body with tags written inline — no title, no Source. The Microblog's content unit, as the Finding is the platform's.
_Avoid_: Post, note, status

**Tag**:
A one-word label attached to content for discovery, written #name; canonical form is lowercase letters and digits (1–50 characters), and anything a user types is folded into it. Free-form and unowned — a tag exists exactly as long as content carries it, in one namespace shared by every content type.
_Avoid_: Hashtag, category, label

**Tag Page**:
The feed of all content carrying one tag, every content type combined in a single stream.
_Avoid_: Tag feed, tag listing

**Observed Feed**:
The signed-in feed collecting the newest content from a user's Observations — their observed tags and observed users, every content type combined.
_Avoid_: My feed, home feed, personalized feed

### Voting

**Dig**:
An upvote on a finding (Wykop's *wykop*).
_Avoid_: Upvote (in UI copy), like

**Bury**:
A downvote on a finding, counting against its promotion (Wykop's *zakop*).
_Avoid_: Downvote (in UI copy), dislike

**Net Score**:
The votes for minus the votes against — a finding's digs minus its buries, or a comment's upvotes minus its downvotes.
_Avoid_: Rating, karma

**Bury Reason**:
The justification every bury carries, one of a closed list: Duplicate, Spam, False information, Inappropriate content, Unsuitable. A voting signal only — never a Report and never seen by moderation.
_Avoid_: Report reason, downvote reason, flag

**Plus**:
The single vote on Microblog content — one retractable vote for an Entry or Entry Comment, with no down direction, no reason, and no self-vote; a score is simply the count of plusses. Never drives promotion (Wykop's *plus*).
_Avoid_: Upvote (for Microblog content), like, dig

### Discussion

**Comment**:
A user-authored text response attached to a finding; the unit of discussion.
_Avoid_: Reply (for top-level), discussion entry

**Upvote**:
A vote for a comment (comments only — findings use Dig).
_Avoid_: Dig (for comments), plus, like

**Downvote**:
A vote against a comment (comments only — findings use Bury).
_Avoid_: Bury (for comments), minus, dislike

**Reply**:
A comment attached to a top-level comment rather than directly to the finding. A reply can never have replies — threads are exactly one level deep.
_Avoid_: Nested comment, sub-comment, thread

**Thread**:
A top-level comment together with its replies, displayed as one unit of a finding's discussion. A thread may have no replies, and a finding may have no threads at all.
_Avoid_: Comment tree, conversation

**Entry Comment**:
A user-authored text response attached to an Entry. Entry discussions are flat: every entry comment attaches directly to the Entry, exactly one level deep, listed oldest-first; addressing another commenter is by "@username" convention, not nesting.
_Avoid_: Reply (Microblog-side), Comment (unqualified, Microblog-side), response

### Documents

**Statute**:
The versioned document stating what the service is for, the rules of conduct, and the consequences of breaking them. Amendments create a new version; old versions remain readable.
_Avoid_: Terms of Service, Rules, Regulations

**Statute Point**:
A single numbered provision of the Statute. Only points flagged as reportable can be cited by a Report.
_Avoid_: Rule, clause, article

**Privacy Policy**:
The versioned document describing what personal data the service processes, why, and the rights users have over it. Separate from the Statute.
_Avoid_: GDPR page, data policy

### Reporting & Moderation

**Report**:
A user's formal claim that a specific finding or comment violates a specific reportable Statute Point, optionally explained with a short note. Feeds moderation only — never scores or promotion.
_Avoid_: Flag, complaint, bury

**Case**:
One reported finding or comment together with all its pending Reports, judged by a moderator as a single unit.
_Avoid_: Ticket, queue entry

**Verdict**:
The moderator's per-case ruling that resolves every pending report on the content at once: Upheld or Dismissed.
_Avoid_: Resolution, judgment

**Removal**:
The moderation action that withdraws a finding or comment from public view, leaving a Tombstone. Reversible by Restore.
_Avoid_: Deletion, takedown

**Tombstone**:
The placeholder shown where removed content used to be — in a thread for a comment, on the detail page for a finding.
_Avoid_: Deleted marker, stub

**Redaction**:
A moderator's text-only edit of a comment's text or a finding's title/description, visibly marked as moderated, with the original preserved internally.
_Avoid_: Mod edit, censoring

**Restore**:
The reversal of a Removal — the tombstone lifts and the content returns as it was.
_Avoid_: Undelete, republish

**Ban**:
A temporary, moderator-imposed block on all of a user's write actions, citing a Statute Point; reading stays open. At most one ban is active per user; it can be lifted early or replaced.
_Avoid_: Suspension, block, timeout

**Moderation Log**:
The internal record of every moderation action — actor, target, cited Statute Point, note, and prior state. Visible to moderators only.
_Avoid_: Audit trail, history

### Users

**Member**:
The default role of every user.
_Avoid_: Regular user, normal user

**Moderator**:
The role empowered to judge Cases and apply moderation actions — never on their own content, and never against another moderator.
_Avoid_: Mod, admin

**Erasure**:
The GDPR removal of a user: their findings and comments survive attributed to the Deleted Account, their cast votes survive anonymized so no score changes, and their pending reports are dropped.
_Avoid_: Account deletion, wipe

**Deleted Account**:
The placeholder author shown on content whose author was erased.
_Avoid_: Anonymous, ghost user

### Social

**Profile**:
A Member's public page: who they are and what they have published (Wykop's */ludzie/{username}* page).
_Avoid_: Account page, user page

**Observation**:
The one-way following relationship from a user to a user or a tag — no approval, freely retractable (Wykop's *obserwacja*). To observe is to hold one.
_Avoid_: Follow, subscription, watch

**Observer**:
A user holding an Observation of a given user or tag. Observer counts are public wherever observing is offered.
_Avoid_: Follower, subscriber

**Mention**:
An @username reference written in discussion text, linking to the named user's Profile and notifying them (Wykop's *przywołanie*).
_Avoid_: Tag (for people), ping, callout

**Notification**:
The per-user inbox item recording that someone interacted with you — commented on your content, replied to you, or mentioned you. Read or unread, never public.
_Avoid_: Alert, ping, message
