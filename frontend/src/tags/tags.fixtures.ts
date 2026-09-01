import { FindingSummaryDto } from '../main-page/main-page-feed.service';
import { TagPageDto, TaggedContentRefDto, TaggedContentType } from './tags.service';

// Shared test data for the tags specs (page, store, services). The server already orders the
// stream — newest created-at first — and the frontend renders it as-is and never re-sorts.

export const contentId = (index: number): string =>
  `00000000-0000-0000-0077-${String(index).padStart(12, '0')}`;

export const ref = (index: number, type: TaggedContentType = 'finding'): TaggedContentRefDto => ({
  type,
  id: contentId(index),
});

export const tagPage = (
  refs: TaggedContentRefDto[],
  hasNextPage = false,
): TagPageDto => ({
  items: refs,
  hasNextPage,
});

export const card = (
  index: number,
  overrides: Partial<FindingSummaryDto> = {},
): FindingSummaryDto => ({
  id: contentId(index),
  title: `Finding ${index}`,
  description: `Finding ${index} — description`,
  sourceUrl: `https://blog.example.org/posts/${index}`,
  domain: 'blog.example.org',
  thumbnailUrl: null,
  author: 'grace_hopper',
  tags: ['dotnet'],
  digCount: 100 + index,
  commentCount: index,
  createdAt: '2026-07-08T06:00:00Z',
  promotedAt: '2026-07-08T09:30:00Z',
  ...overrides,
});
