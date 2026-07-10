# Podkop is a 1:1 functional copy of Wykop

Podkop's functionality deliberately mirrors Wykop's observable behavior one-to-one: when a design question arises (what a feed shows, where a click goes, what number a card displays), the answer is "what Wykop does", not what Reddit/HN does or what seems simplest. This resolves product debates cheaply and keeps the domain language anchored to a real, proven reference — at the cost of ruling out "improvements" that deviate from Wykop's behavior, even appealing ones (e.g. net-score display on cards, title linking to an internal detail page).

Implementation and technology are NOT copied — only functionality. Where Wykop's internals are unobservable (e.g. its secret promotion algorithm), we implement the simplest rule that reproduces the observable behavior.
