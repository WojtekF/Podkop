import { tapResponse } from '@ngrx/operators';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { pipe, switchMap, tap } from 'rxjs';
import { signalStore, withMethods, withState, patchState } from '@ngrx/signals';
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
    const load = rxMethod<string>(
      pipe(
        tap({
          next: (id: string) => {
            patchState(store, { status: 'loading', id, finding: null });
          },
        }),
        switchMap((id: string) =>
          service.getFinding(id).pipe(
            tapResponse({
              next: (finding: FindingDetail) => {
                patchState(store, { status: 'loaded', finding });
              },
              error: (error: HttpErrorResponse) => {
                const status = error.status;
                patchState(store, { status: status === 404 ? 'notFound' : 'error' });
              },
            }),
          ),
        ),
      ),
    );

    const retry = (): void => {
      const id = store.id();
      if (id !== null) load(id);
    };

    return {
      load,
      retry,
    };
  }),
);
