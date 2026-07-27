import { tapResponse } from '@ngrx/operators';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { pipe, switchMap, tap } from 'rxjs';
import { signalStore, withMethods, withState, patchState } from '@ngrx/signals';
import { CommentThreadDto, FindingCommentsService } from './finding-comments.service';
import { FindingDetailDto, FindingDetailService } from './finding-detail.service';

export type FindingDetailStatus = 'loading' | 'loaded' | 'notFound' | 'error';

export interface FindingDetailState {
  id: string | null;
  finding: FindingDetailDto | null;
  comments: CommentThreadDto[] | null;
  status: FindingDetailStatus;
}

const initialState: FindingDetailState = {
  id: null,
  finding: null,
  comments: null,
  status: 'loading',
};

export const FindingDetailStore = signalStore(
  withState(initialState),
  // Issue #16: load(id) must now fetch the finding AND its discussion in parallel and land on
  // exactly one terminal status only once BOTH answers are in — 'loaded' holding the finding
  // and its comment threads (kept exactly in the order the server sent them), 'notFound' when
  // either request 404s, 'error' for any other failure on either. retry() re-runs both for
  // the id currently held. See finding-detail.store.spec.ts for the transitions to satisfy.
  withMethods((store, service = inject(FindingDetailService), commentsService = inject(FindingCommentsService)) => {
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
              next: (finding: FindingDetailDto) => {
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
