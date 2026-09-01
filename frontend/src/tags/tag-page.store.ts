import { signalStore, withMethods, withState } from '@ngrx/signals';
import { inject } from '@angular/core';
import { FindingSummaryDto } from '../main-page/main-page-feed.service';
import { TagContentFilter, TagsService } from './tags.service';
import { TagHydrationService } from './tag-hydration.service';

export type TagPageStatus = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * One rendered row of a tag page: a reference that hydrated, paired with whatever card the owning
 * slice answered for it. Keeping the type alongside the card is what lets the page render a
 * combined stream — a row knows which component to draw itself with.
 */
export type TagStreamItem = { type: 'finding'; finding: FindingSummaryDto };

export interface TagPageState {
  /** The tag exactly as the URL spelled it — the page header shows the canonical form it resolved to. */
  name: string | null;
  filter: TagContentFilter;
  page: number;
  /** The hydrated stream, already in the index's order. */
  items: readonly TagStreamItem[];
  hasNextPage: boolean;
  status: TagPageStatus;
}

const initialState: TagPageState = {
  name: null,
  filter: 'all',
  page: 1,
  items: [],
  hasNextPage: false,
  status: 'loading',
};

/**
 * The Tag Page's state (issue #77). One load is two steps, in this order and no other: the Tags
 * endpoint answers an ordered page of typed references, then the references are hydrated per
 * content type through the owning slices' batch endpoints — the second call cannot be issued
 * until the first has answered, because its ids are what the first returned.
 *
 * What the loaded state has to hold to be right:
 * - the stream in the index's order, never re-sorted client-side: the server decided Newest;
 * - references that hydrated to nothing dropped from it (ADR 0011), so a page may render short;
 * - a page whose references all hydrate to nothing is still a loaded, merely empty page — not an
 *   error and not a not-found;
 * - a 404 from the Tags endpoint as its own state, distinct from a load failure: the tag does not
 *   exist, and there is nothing to retry;
 * - a failed hydration as a load failure, because a page of cards that cannot be drawn is not a
 *   page the reader can use.
 */
export const TagPageStore = signalStore(
  withState(initialState),

  withMethods((
    store,
    tags = inject(TagsService),
    hydration = inject(TagHydrationService),
  ) => {
    /** Loads one tag page: the references, then the cards they name. */
    const load = (name: string, filter: TagContentFilter, page: number): void => {
      throw new Error('not implemented');
    };

    /** Retries the load the page is currently showing — for the error state only. */
    const retry = (): void => {
      throw new Error('not implemented');
    };

    return { load, retry };
  }),
);
