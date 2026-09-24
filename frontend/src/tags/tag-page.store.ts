import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { inject } from '@angular/core';
import { FindingSummaryDto } from '../main-page/main-page-feed.service';
import { TagContentFilter, TagsService } from './tags.service';
import { TagHydrationService } from './tag-hydration.service';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, of, pipe, switchMap, tap } from 'rxjs';
import { asResult, isLoadFailure, isNotFound } from '../shared/as-result';

export type TagPageStatus = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * One rendered row of a tag page: a reference that hydrated, paired with whatever card the owning
 * slice answered for it. Keeping the type alongside the card is what lets the page render a
 * combined stream — a row knows which component to draw itself with.
 */
export type TagStreamItem = { type: 'finding'; finding: FindingSummaryDto };

export interface TagPageState {
  /**
   * The tag's name. While a page loads it is the name exactly as the URL spelled it; once the Tags
   * endpoint answers it becomes the canonical name the server resolved that spelling to, which is
   * what the header shows. A 404 or a failed Tags call leaves the URL's spelling in place, since
   * nothing was resolved.
   */
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

  withMethods((store, tags = inject(TagsService), hydration = inject(TagHydrationService)) => {
    type LoadRequest = { name: string; filter: TagContentFilter; page: number };

    /** Loads one tag page: the references, then the cards they name. */
    const loadRequest = rxMethod<LoadRequest>(
      pipe(
        tap(({ filter, name, page }) =>
          patchState(store, { name, filter, page, status: 'loading', hasNextPage: false }),
        ),
        switchMap(({ filter, name, page }) => {
          return asResult(tags.getTagPage(name, filter, page)).pipe(
            switchMap((tagPage) => {
              if (isNotFound(tagPage)) {
                patchState(store, { status: 'notFound', items: [] });
              } else if (isLoadFailure(tagPage)) {
                patchState(store, { status: 'error', items: [] });
              } else {
                const ids = tagPage.items.filter((i) => i.type === 'finding').map((i) => i.id);

                const cards$ = ids.length ? asResult(hydration.getFindingsByIds(ids)) : of([]);

                return cards$.pipe(
                  tap({
                    next: (cards) => {
                      if (isLoadFailure(cards)) {
                        patchState(store, {
                          status: 'error',
                          items: [],
                          name: tagPage.name,
                        });
                      } else {
                        patchState(store, {
                          hasNextPage: tagPage.hasNextPage,
                          items: tagPage.items.flatMap((item) => {
                            const finding = cards.find(
                              (c) => c.id === item.id && item.type === 'finding',
                            );
                            return finding ? [{ type: 'finding' as const, finding }] : [];
                          }),
                          status: 'loaded',
                          name: tagPage.name,
                        });
                      }
                    },
                  }),
                );
              }

              return EMPTY;
            }),
          );
        }),
      ),
    );

    const load = (name: string, filter: TagContentFilter, page: number): void => {
      loadRequest({ name, filter, page });
    };

    /** Retries the load the page is currently showing — for the error state only. */
    const retry = (): void => {
      load(store.name()!, store.filter(), store.page());
    };

    return { load, retry };
  }),
);
