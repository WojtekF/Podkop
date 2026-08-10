# Statute is versioned structured data; reports pin a point and a version

The Statute and the Privacy Policy are structured, versioned documents — sections of numbered points with stable IDs and effective-from dates, shipped as read-only seeded content — rather than static pages, because a Report cites the specific Statute Point it claims was broken. Every report stores the point ID plus the statute version current at filing time, so amendments (renumbering, rewording, or removing points) never falsify or orphan already-filed reports.

## Considered Options

- **Static page + closed reason enum** (how Bury Reason works, and closest to Wykop's observable report modal) — rejected: abandons the requirement that reporters pick the actual broken point.
- **Structured but unversioned** — rejected: an amendment would silently change what old reports "meant", and notice-of-change good practice (effective dates) would have nowhere to hang.
