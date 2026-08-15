import { CaseSummaryDto } from './moderation.service';

// Shared test data for the moderation specs (store, service, case queue page).

/**
 * The case queue as the server serves it (issue #34): ordered oldest grievance first, and the
 * frontend renders that order as-is — it never re-sorts. The fixture's order is deliberately
 * none of the plausible wrong ones: not by report count (the comment case has the most), not
 * newest-first (the last case is the newest), not by author or target id — so any client-side
 * re-sorting fails the specs. The second case is a comment: its findingId names the finding it
 * lives on, not itself. The third case's author is the stub acting moderator — cases about a
 * moderator's own content stay listed. The two reports of the comment case cite the same point
 * across an amendment, so the same "2.1" carries each pinned version's own wording.
 */
export const caseQueue = (): CaseSummaryDto[] => [
  {
    targetKind: 'Finding',
    targetId: 'f0000000-0000-4000-8000-000000000001',
    findingId: 'f0000000-0000-4000-8000-000000000001',
    preview: 'A finding under scrutiny',
    author: 'margaret_h',
    reportCount: 1,
    reports: [
      {
        pointCitation: '2.3',
        pointText: 'Do not post hateful content.',
        note: 'Harassing tone.',
        filedAt: '2026-08-01T09:00:00Z',
      },
    ],
  },
  {
    targetKind: 'Comment',
    targetId: 'c0000000-0000-4000-8000-000000000009',
    findingId: 'f0000000-0000-4000-8000-000000000001',
    preview: 'A comment under scrutiny.',
    author: 'grace_hopper',
    reportCount: 2,
    reports: [
      {
        pointCitation: '2.1',
        pointText: 'Do not post spam. (v1)',
        note: 'Links a spam farm.',
        filedAt: '2026-08-01T11:00:00Z',
      },
      {
        pointCitation: '2.1',
        pointText: 'Do not post spam. (v2)',
        note: null,
        filedAt: '2026-08-03T08:30:00Z',
      },
    ],
  },
  {
    targetKind: 'Finding',
    targetId: 'a0000000-0000-4000-8000-000000000003',
    findingId: 'a0000000-0000-4000-8000-000000000003',
    preview: "The moderator's own finding",
    author: 'ada_lovelace',
    reportCount: 1,
    reports: [
      {
        pointCitation: '2.1',
        pointText: 'Do not post spam. (v2)',
        note: null,
        filedAt: '2026-08-02T12:00:00Z',
      },
    ],
  },
];
