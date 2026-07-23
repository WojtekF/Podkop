import { FindingDetail } from './finding-detail.service';

// Shared test data for the finding-detail specs (component, store, service).
export const findingId = '0d4f9a3e-1111-4222-8333-444455556666';

export const findingDetail = (overrides: Partial<FindingDetail> = {}): FindingDetail => ({
  id: findingId,
  title: 'A remarkable finding',
  description: 'The full, untruncated description of the finding.',
  sourceUrl: 'https://blog.example.org/posts/42',
  domain: 'blog.example.org',
  thumbnailUrl: 'https://example.com/thumb.jpg',
  author: 'ada_lovelace',
  tags: ['angular', 'webdev'],
  digCount: 123,
  commentCount: 9,
  createdAt: '2026-07-08T03:30:00Z',
  promotedAt: '2026-07-08T09:30:00Z',
  ...overrides,
});
