# Podkop

A Wykop-style social link-aggregation platform: users submit findings, vote on them, and the best ones get promoted to the main page.

## Language

**Finding**:
A link (or content) submitted by a user for the community to vote on. The central content unit of the platform.
_Avoid_: Post, sink item

**Feed**:
An ordered, pageable listing of findings selected by a rule. The platform has two: the Main Page (promoted findings) and Upcoming (fresh findings).
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

**Dig**:
An upvote on a finding (Wykop's *wykop*).
_Avoid_: Upvote (in UI copy), like

**Bury**:
A downvote on a finding, counting against its promotion (Wykop's *zakop*).
_Avoid_: Downvote (in UI copy), dislike

**Net Score**:
A finding's digs minus its buries.
_Avoid_: Rating, karma
