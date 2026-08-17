import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { CaseSummaryDto, ModerationService } from './moderation.service';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { asResult, isLoadFailure } from '../shared/as-result';
import { tapResponse } from '@ngrx/operators';

export type CaseQueueStatus = 'loading' | 'loaded' | 'error';

export interface CaseQueueState {
  cases: CaseSummaryDto[] | null;
  status: CaseQueueStatus;
}

const initialState: CaseQueueState = {
  cases: null,
  status: 'loading',
};

/**
 * The moderator case queue's state (issue #34): the one fetch of the open cases, exposed as
 * signals the queue page renders from. `cases` holds the queue exactly as served — the server
 * owns the oldest-grievance-first order and the page never re-sorts.
 */
export const CaseQueueStore = signalStore(
  withState(initialState),
  withMethods((store, service = inject(ModerationService)) => {
    /**
     * Loads the queue of open cases through the ModerationService: entering the loading
     * state, then landing loaded with the cases as served, or error on any failure — the
     * moderators-only refusal included.
     */
    const load = rxMethod<void>(
      pipe(
        switchMap(() => {
          return asResult(service.getCaseQueue()).pipe(
            tap({
              next: (response) => {
                if (isLoadFailure(response)) {
                  patchState(store, { status: 'error' });
                } else {
                  patchState(store, { status: 'loaded', cases: response });
                }
              },
            }),
          );
        }),
      ),
    );

    return { load };
  }),
);
