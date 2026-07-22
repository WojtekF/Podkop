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

export const MainPageStore = signalStore(
  withState(initialState),
  withComputed(({ items, page, status }) => ({
    hasPreviousPage: computed<boolean>(() => {
      return page() > 1;
    }),
    isEmpty: computed<boolean>(() => {
      return status() === 'loaded' && items().length === 0;
    }),
  })),

  withMethods((store, feedService = inject(MainPageFeedService)) => {
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

    const retry = (): void => {
      loadPage(store.page());
    };

    return {
      retry,
      loadPage,
    };
  }),
);
