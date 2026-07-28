import { LoadResult, asResult } from './as-result';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, pipe, switchMap, tap, TimeoutError } from 'rxjs';
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
  withMethods(
    (
      store,
      service = inject(FindingDetailService),
      commentsService = inject(FindingCommentsService),
    ) => {
      const load = rxMethod<string>(
        pipe(
          tap({
            next: (id: string) => {
              patchState(store, { status: 'loading', id, finding: null, comments: null });
            },
          }),
          switchMap((id) =>
            forkJoin({
              finding: asResult(service.getFinding(id)),
              comments: asResult(commentsService.getComments(id)),
            }).pipe(
              tap({
                next: ({ finding, comments }) => {
                  patchState(store, toPatch(finding, comments));
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
    },
  ),
);

function toPatch(
  finding: LoadResult<FindingDetailDto>,
  comments: LoadResult<CommentThreadDto[]>,
): Partial<FindingDetailState> {
  if (isNotFound(finding) || isNotFound(comments)) return { status: 'notFound' };
  if (
    finding instanceof HttpErrorResponse ||
    comments instanceof HttpErrorResponse ||
    finding instanceof TimeoutError ||
    comments instanceof TimeoutError
  )
    return { status: 'error' };
  return { status: 'loaded', finding, comments };
}

function isNotFound<T>(input: T | HttpErrorResponse): boolean {
  if (input instanceof HttpErrorResponse) {
    return input.status === 404;
  }
  return false;
}
