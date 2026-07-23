import { inject } from '@angular/core';
import { signalStore, withMethods, withState } from '@ngrx/signals';
import { FindingDetail, FindingDetailService } from './finding-detail.service';

export type FindingDetailStatus = 'loading' | 'loaded' | 'notFound' | 'error';

export interface FindingDetailState {
  id: string | null;
  finding: FindingDetail | null;
  status: FindingDetailStatus;
}

const initialState: FindingDetailState = {
  id: null,
  finding: null,
  status: 'loading',
};

export const FindingDetailStore = signalStore(
  withState(initialState),
  withMethods((store, service = inject(FindingDetailService)) => {
    // load(id): remember the id, move to 'loading', fetch the finding, then land on exactly
    // one terminal status — 'loaded' holding the finding, 'notFound' when the fetch 404s, or
    // 'error' for any other failure. retry(): re-run the load for the id currently held.
    // See finding-detail.store.spec.ts for the transitions to satisfy.
    const load = (id: string): void => {
      throw new Error('not implemented');
    };

    const retry = (): void => {
      throw new Error('not implemented');
    };

    return {
      load,
      retry,
    };
  }),
);
