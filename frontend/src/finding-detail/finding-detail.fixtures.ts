import { CommentThreadDto } from './finding-comments.service';
import { FindingDetailDto } from './finding-detail.service';

// Shared test data for the finding-detail specs (component, store, service).
export const findingId = '0d4f9a3e-1111-4222-8333-444455556666';

export const findingDetail = (overrides: Partial<FindingDetailDto> = {}): FindingDetailDto => ({
  id: findingId,
  title: 'A remarkable finding',
  description: 'The full, untruncated description of the finding.',
  sourceUrl: 'https://blog.example.org/posts/42',
  domain: 'blog.example.org',
  thumbnailUrl: 'https://example.com/thumb.jpg',
  author: 'ada_lovelace',
  tags: ['angular', 'webdev'],
  digCount: 123,
  myVote: null,
  commentCount: 9,
  createdAt: '2026-07-08T03:30:00Z',
  promotedAt: '2026-07-08T09:30:00Z',
  ...overrides,
});

// The discussion the comments endpoint returns for the finding: already ordered by the
// server — threads best-first, replies chronological. The frontend renders this order as-is.
// The reader's votes are scattered the way the seeds scatter them (issue #18): one thread
// voted up, one reply voted down (its counts include that vote), the reader's own comment
// (ada_lovelace) unvotable, and one fresh thread with no vote yet.
export const commentThreads = (): CommentThreadDto[] => [
  {
    id: 'c0000000-0000-4000-8000-000000000001',
    author: 'grace_hopper',
    text: 'Best take in the thread.',
    upvoteCount: 12,
    downvoteCount: 2,
    myVote: 'up',
    createdAt: '2026-07-08T10:00:00Z',
    replies: [
      {
        id: 'c0000000-0000-4000-8000-000000000011',
        author: 'linus_t',
        text: 'Agreed — with a caveat.',
        upvoteCount: 1,
        downvoteCount: 1,
        myVote: 'down',
        createdAt: '2026-07-08T10:30:00Z',
      },
      {
        id: 'c0000000-0000-4000-8000-000000000012',
        author: 'ada_lovelace',
        text: 'The caveat does not hold up.',
        upvoteCount: 5,
        downvoteCount: 1,
        myVote: null,
        createdAt: '2026-07-08T11:00:00Z',
      },
    ],
  },
  {
    id: 'c0000000-0000-4000-8000-000000000002',
    author: 'margaret_h',
    text: 'A dissenting view, sitting lower.',
    upvoteCount: 3,
    downvoteCount: 4,
    myVote: null,
    createdAt: '2026-07-07T09:00:00Z',
    replies: [],
  },
];
