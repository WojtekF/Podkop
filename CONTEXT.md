# Podkop

A Wykop-style social link-aggregation platform: users submit findings, vote on them, and the best ones get promoted to the main page. Functionality is intended to be a 1:1 copy of Wykop.

## Language

**Finding**:
A link (or content) submitted by a user for the community to vote on. The central content unit of the platform.
_Avoid_: Post, item, card, sink item

**Main Page**:
The feed showing only Promoted findings. This is the site's front page.
_Avoid_: Home feed, all-posts feed

**Upcoming**:
The feed showing all fresh, not-yet-promoted findings (Wykop's *Wykopalisko*).
_Avoid_: New, pending

**Promotion**:
The one-way transition of a finding from Upcoming to the Main Page, earned when its net score reaches the promotion threshold. Once promoted, a finding stays promoted.
_Avoid_: Featuring, trending

**Source**:
The external URL a finding points to. A finding's displayed domain is derived from its source, and the finding's title links to it.
_Avoid_: Link, target

**Dig**:
An upvote on a finding (Wykop's *wykop*). The dig count is the finding's public headline number.
_Avoid_: Upvote (in UI copy), like

**Net Score**:
A finding's digs minus its buries. Used internally to trigger promotion; never displayed.
_Avoid_: Rating, karma

**Bury**:
A downvote on a finding, counting against its promotion (Wykop's *zakop*).
_Avoid_: Downvote (in UI copy), dislike
