import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, forkJoin, of, pipe, switchMap, tap } from 'rxjs';
import { signalStore, withMethods, withState, patchState } from '@ngrx/signals';
import { CommentThreadDto, FindingCommentsService } from './finding-comments.service';
import { FindingDetailDto, FindingDetailService } from './finding-detail.service';
import { tapResponse } from '@ngrx/operators';

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
              patchState(store, { status: 'loading', id, finding: null });
            },
          }),
          switchMap((id) =>
            forkJoin({
              finding: service
                .getFinding(id)
                .pipe(catchError((error: HttpErrorResponse) => of(error))),
              comments: commentsService
                .getComments(id)
                .pipe(catchError((error: HttpErrorResponse) => of(error))),
            }).pipe(
              tapResponse({
                next: ({ finding, comments }) => {
                  patchState(store, toPatch(finding, comments));
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
    },
  ),
);
function toPatch(
  finding: FindingDetailDto | HttpErrorResponse,
  comments: CommentThreadDto[] | HttpErrorResponse,
): Partial<FindingDetailState> {
  if (isNotFound(finding) || isNotFound(comments)) return { status: 'notFound' };
  if (finding instanceof HttpErrorResponse || comments instanceof HttpErrorResponse)
    return { status: 'error' };
  return { status: 'loaded', finding, comments }; // ✅ both narrowed to DTOs here
}

function isNotFound<T>(input: T | HttpErrorResponse): boolean {
  if (input instanceof HttpErrorResponse) {
    return input.status === 404;
  }
  return false;
}
