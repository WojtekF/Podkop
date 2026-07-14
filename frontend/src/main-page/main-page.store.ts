import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { FeedPage, FindingSummary, MainPageFeedService } from './main-page-feed.service';
import { pipe, switchMap, tap } from 'rxjs';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';

export type MainPageStatus = 'loading' | 'loaded' | 'error';

export interface MainPageState {
  items: FindingSummary[];
  page: number;
  hasNextPage: boolean;
  status: MainPageStatus;
}

const initialState: MainPageState = {
  items: [],
  page: 1,
  hasNextPage: false,
  status: 'loading',
};

/**
 * Component-provided store for the Main Page feed (issue #7).
 *
 * Pages replace each other (no accumulation), and `loadPage` must cancel any
 * in-flight request when a new page is asked for. Computed and method bodies
 * are implemented by the user (CLAUDE.md Feature Development Workflow).
 */
export const MainPageStore = signalStore(
  withState(initialState),
  // withComputed receives the state as signals (destructured here); derive
  // with computed(() => ...) reading them, e.g. page() > 1.
  withComputed(({ items, page, status }) => ({
    /** True beyond page 1 — drives the Previous button and the stale-deep-link action. */
    hasPreviousPage: computed<boolean>(() => {
      return page() > 1;
    }),
    /** True only when a *loaded* page has no items (never while loading/error). */
    isEmpty: computed<boolean>(() => {
      return status() === 'loaded' && items().length === 0;
    }),
  })),
  // Update state with patchState(store, { ... }) (from '@ngrx/signals').
  // For loadPage's cancel-the-previous-request rule, the idiomatic tool is
  // rxMethod (from '@ngrx/signals/rxjs-interop') with switchMap over
  // feedService.getPage(...) — switchMap unsubscribes the in-flight HTTP call
  // when a new page number arrives. A manually managed Subscription you
  // .unsubscribe() before each new call works too.
  withMethods((store, feedService = inject(MainPageFeedService)) => {
    /** Fetch one page; on success status='loaded' + replace items; on failure status='error'. */
    const loadPage = rxMethod<number>(
      pipe(
        tap((page: number) => {
          patchState(store, { status: 'loading', page });
        }),
        switchMap((page: number) =>
          feedService.getPage(page).pipe(
            tapResponse({
              next: ({ hasNextPage, items }: FeedPage) => {
                patchState(store, {
                  status: 'loaded',
                  hasNextPage,
                  items,
                });
              },
              error: () => {
                patchState(store, { items: [], status: 'error' });
              },
            }),
          ),
        ),
      ),
    );

    /** Re-request the page the store is currently on (after an error). */
    const retry = (): void => {
      loadPage(store.page());
    };

    return {
      retry,
      loadPage,
    };
  }),
);
