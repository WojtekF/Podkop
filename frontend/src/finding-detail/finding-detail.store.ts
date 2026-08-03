import { LoadResult, asResult } from './as-result';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { concatMap, exhaustMap, forkJoin, pipe, switchMap, tap, TimeoutError } from 'rxjs';
import { signalStore, withMethods, withState, patchState } from '@ngrx/signals';
import {
  CommentThreadDto,
  CommentVoteDirection,
  CommentVotesDto,
  FindingCommentsService,
} from './finding-comments.service';
import {
  FindingDetailDto,
  FindingDetailService,
  FindingVoteIntent,
} from './finding-detail.service';
import { tapResponse } from '@ngrx/operators';
import { MatSnackBar } from '@angular/material/snack-bar';

export type FindingDetailStatus = 'loading' | 'loaded' | 'notFound' | 'error';

export interface FindingDetailState {
  id: string | null;
  finding: FindingDetailDto | null;
  comments: CommentThreadDto[] | null;
  status: FindingDetailStatus;
  pendingCommentVoteIds: readonly string[];
  pendingFindingVote: boolean;
}

const initialState: FindingDetailState = {
  id: null,
  finding: null,
  comments: null,
  status: 'loading',
  pendingCommentVoteIds: [],
  pendingFindingVote: false,
};

export const FindingDetailStore = signalStore(
  withState(initialState),
  withMethods(
    (
      store,
      service = inject(FindingDetailService),
      commentsService = inject(FindingCommentsService),
      snackBar = inject(MatSnackBar),
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

      const voteOnComment = rxMethod<{ commentId: string; direction: CommentVoteDirection }>(
        pipe(
          tap({
            next: ({ commentId }) => {
              patchState(store, {
                pendingCommentVoteIds: [...store.pendingCommentVoteIds(), commentId],
              });
            },
          }),
          concatMap(({ commentId, direction }) => {
            const request$ =
              myVoteOf(store.comments(), commentId) === direction
                ? commentsService.withdrawMyVote(commentId)
                : commentsService.setMyVote(commentId, direction);

            return request$.pipe(
              tapResponse({
                next: (votes) => {
                  patchState(store, {
                    comments: applyVotes(store.comments()!, commentId, votes),
                    pendingCommentVoteIds: filterFromPendingVotes(
                      store.pendingCommentVoteIds(),
                      commentId,
                    ),
                  });
                },
                error: () => {
                  patchState(store, {
                    pendingCommentVoteIds: filterFromPendingVotes(
                      store.pendingCommentVoteIds(),
                      commentId,
                    ),
                  });
                  snackBar.open("Couldn't vote on comment. Please try again.");
                },
              }),
            );
          }),
        ),
      );

      const voteOnFinding = rxMethod<FindingVoteIntent>(
        pipe(
          tap(() => {
            patchState(store, { pendingFindingVote: true });
          }),
          exhaustMap((intent) => {
            const request$ =
              intent.type === store.finding()?.myVote
                ? service.withdrawMyVote(store.id()!)
                : service.setMyVote(store.id()!, intent);

            return request$.pipe(
              tapResponse({
                next: (result) => {
                  patchState(store, {
                    finding: { ...store.finding()!, ...result },
                    pendingFindingVote: false,
                  });
                },
                error: () => {
                  patchState(store, { pendingFindingVote: false });
                  snackBar.open("Couldn't vote on finding. Please try again.");
                },
              }),
            );
          }),
        ),
      );

      return {
        load,
        retry,
        voteOnComment,
        voteOnFinding,
      };
    },
  ),
);

const filterFromPendingVotes = (pendingVotes: readonly string[], commentId: string) =>
  pendingVotes.filter((votes) => votes !== commentId);

const myVoteOf = (threads: CommentThreadDto[] | null, commentId: string) => {
  const rows = threads?.flatMap((thread) => [thread, ...thread.replies]);
  return rows?.find((row) => row.id === commentId)?.myVote ?? null;
};

const applyVotes = (
  threads: CommentThreadDto[],
  commentId: string,
  votes: CommentVotesDto,
): CommentThreadDto[] => {
  return threads.map((thread) =>
    thread.id === commentId
      ? { ...thread, ...votes }
      : {
          ...thread,
          replies: thread.replies.map((reply) =>
            reply.id === commentId ? { ...reply, ...votes } : reply,
          ),
        },
  );
};

const toPatch = (
  finding: LoadResult<FindingDetailDto>,
  comments: LoadResult<CommentThreadDto[]>,
): Partial<FindingDetailState> => {
  if (isNotFound(finding) || isNotFound(comments)) return { status: 'notFound' };
  if (
    finding instanceof HttpErrorResponse ||
    comments instanceof HttpErrorResponse ||
    finding instanceof TimeoutError ||
    comments instanceof TimeoutError
  )
    return { status: 'error' };
  return { status: 'loaded', finding, comments };
};

const isNotFound = <T>(input: T | HttpErrorResponse): boolean => {
  if (input instanceof HttpErrorResponse) {
    return input.status === 404;
  }
  return false;
};
