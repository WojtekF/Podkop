import { computed, inject } from '@angular/core';
import { signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { FindingSummary, MainPageFeedService } from './main-page-feed.service';

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
  withComputed(({ items, page, status }) => ({
    hasPreviousPage: computed<boolean>(() => {
      throw new Error('not implemented');
    }),
    isEmpty: computed<boolean>(() => {
      throw new Error('not implemented');
    }),
  })),
  withMethods((store, feedService = inject(MainPageFeedService)) => ({
    loadPage(page: number): void {
      throw new Error('not implemented');
    },
    retry(): void {
      throw new Error('not implemented');
    },
  })),
);
