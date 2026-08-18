import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { CaseSummaryDto, ModerationService } from './moderation.service';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap, EMPTY, mergeMap } from 'rxjs';
import { asResult, isLoadFailure, isNotFound } from '../shared/as-result';
import { MatSnackBar } from '@angular/material/snack-bar';

export type CaseQueueStatus = 'loading' | 'loaded' | 'error';

/** One case's dismissal intent (issue #35): the target whose open case the moderator dismisses. */
export interface DismissCaseIntent {
  targetKind: CaseSummaryDto['targetKind'];
  targetId: string;
}

export interface CaseQueueState {
  cases: CaseSummaryDto[] | null;
  status: CaseQueueStatus;
  /**
   * `${targetKind}:${targetId}` keys of the cases whose dismissal is in flight (issue #35) —
   * a case's key is present exactly while its request runs.
   */
  pendingDismissKeys: string[];
}

const initialState: CaseQueueState = {
  cases: null,
  status: 'loading',
  pendingDismissKeys: [],
};

/**
 * The moderator case queue's state (issue #34): the one fetch of the open cases, exposed as
 * signals the queue page renders from. `cases` holds the queue exactly as served — the server
 * owns the oldest-grievance-first order and the page never re-sorts. Dismissals (issue #35)
 * resolve cases in place: a dismissed case leaves the queue without a refetch.
 */
export const CaseQueueStore = signalStore(
  withState(initialState),
  withMethods((store, service = inject(ModerationService), snackBar = inject(MatSnackBar)) => {
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

    const getDismissKey = (targetKind: CaseSummaryDto['targetKind'], targetId: string) =>
      `${targetKind}:${targetId}`;

    /**
     * Dismisses one open case through the ModerationService (issue #35). The case's
     * `${targetKind}:${targetId}` key is pending exactly while its request is in flight; a
     * repeat intent for a pending case is dropped whole — no request, no state — while
     * different cases may dismiss in parallel, each tracked by its own key. Success removes
     * exactly that case from the queue, and so does the unknown-case refusal (HTTP 404,
     * problem type `podkop:problem:unknown-case`) — silently, because another moderator
     * resolved the case first. Any other failure keeps the case listed, clears its pending
     * key, and announces itself in a snackbar ("Couldn't dismiss the case.").
     */
    const dismiss = rxMethod<DismissCaseIntent>(
      pipe(
        mergeMap(({ targetId, targetKind }) => {
          if (store.pendingDismissKeys().includes(getDismissKey(targetKind, targetId)))
            return EMPTY;
          patchState(store, {
            pendingDismissKeys: [
              ...store.pendingDismissKeys(),
              getDismissKey(targetKind, targetId),
            ],
          });

          const removeCase = () => {
            patchState(store, {
              pendingDismissKeys: [
                ...store
                  .pendingDismissKeys()
                  .filter((key) => key !== getDismissKey(targetKind, targetId)),
              ],
              cases: [
                ...(store.cases() ?? []).filter(
                  (caseSummary) =>
                    caseSummary.targetId !== targetId || caseSummary.targetKind !== targetKind,
                ),
              ],
            });
          };

          return asResult(service.dismissCase(targetKind, targetId)).pipe(
            tap({
              next: (response) => {
                if (isNotFound(response)) {
                  removeCase();
                } else if (isLoadFailure(response)) {
                  snackBar.open("Couldn't dismiss the case.");
                  patchState(store, {
                    pendingDismissKeys: [
                      ...store
                        .pendingDismissKeys()
                        .filter((key) => key !== getDismissKey(targetKind, targetId)),
                    ],
                  });
                } else {
                  removeCase();
                }
              },
            }),
          );
        }),
      ),
    );

    return { load, dismiss, getDismissKey };
  }),
);
