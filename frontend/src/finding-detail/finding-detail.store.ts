import { LoadResult, asResult } from './as-result';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import {
  concatMap,
  exhaustMap,
  find,
  forkJoin,
  mergeMap,
  pipe,
  switchMap,
  tap,
  TimeoutError,
} from 'rxjs';
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

/**
 * One comment composer's state (issue #17). Composers live in the store keyed by
 * TOP_COMPOSER_KEY (the always-present composer at the top of the comments section) or a
 * top-level comment's id (an inline reply composer — present in the map exactly while open).
 * Each key is independent: drafts and in-flight posts on one never block another.
 */
export interface ComposerState {
  draft: string;
  pending: boolean;
}

export const TOP_COMPOSER_KEY = 'top';

export interface FindingDetailState {
  id: string | null;
  finding: FindingDetailDto | null;
  comments: CommentThreadDto[] | null;
  status: FindingDetailStatus;
  pendingCommentVoteIds: readonly string[];
  pendingFindingVote: boolean;
  composers: Readonly<Record<string, ComposerState>>;
}

const initialState: FindingDetailState = {
  id: null,
  finding: null,
  comments: null,
  status: 'loading',
  pendingCommentVoteIds: [],
  pendingFindingVote: false,
  composers: { [TOP_COMPOSER_KEY]: { draft: '', pending: false } },
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

      /**
       * Opens (or re-targets) the reply composer of one thread (issue #17). With an
       * appendAuthor — the reader answered a reply — `@author ` is appended to whatever
       * draft the composer already holds; nothing typed is ever discarded. Without one the
       * composer just opens (empty on first open), no prefill.
       */
      const openReplyComposer = ({
        appendAuthor,
        threadId,
      }: {
        threadId: string;
        appendAuthor: string | null;
      }): void => {
        const { [threadId]: _opened, ...rest } = store.composers();

        patchState(store, {
          composers: {
            ...rest,

            [threadId]: {
              draft: `${_opened ? _opened.draft + ' ' : ''}${appendAuthor ? `@${appendAuthor} ` : ''}`,
              pending: false,
            },
          },
        });
      };

      /** Records an edit to the composer's draft (issue #17). */
      const updateComposerDraft = (_edit: { composerKey: string; text: string }): void => {
        const { [_edit.composerKey]: _edited, ...rest } = store.composers();
        patchState(store, {
          composers: {
            ...rest,
            ...{ [_edit.composerKey]: { draft: _edit.text, pending: false } },
          },
        });
      };

      /** Closes a reply composer and discards its draft (issue #17). */
      const cancelReplyComposer = (_threadId: string): void => {
        const { [_threadId]: _removed, ...rest } = store.composers();
        let clean = {};
        if (_threadId === TOP_COMPOSER_KEY) {
          clean = { [TOP_COMPOSER_KEY]: { draft: '', pending: false } };
        }
        patchState(store, { composers: { ...clean, ...rest } });
      };

      /**
       * Posts the composer's draft (issue #17). TOP_COMPOSER_KEY posts a top-level comment:
       * on success the created comment is pinned to the top of the thread list for this
       * session (newest post first — real ordering applies from the next load), the draft
       * clears, and the finding's comment count reconciles by +1. A thread id posts a reply:
       * on success it appends to that thread's replies (chronological — last) and the
       * composer closes. Each composer's in-flight state is its own: pending disables only
       * that composer, and posts from different composers may overlap. Failure shows a
       * snackbar and leaves the draft and the discussion untouched.
       */
      const postComment = rxMethod<string>(
        pipe(
          tap((_composerKey) => {
            const { [_composerKey]: _posted, ...rest } = store.composers();
            patchState(store, {
              composers: { ...rest, [_composerKey]: { ..._posted, pending: true } },
            });
          }),
          mergeMap((_composerKey) => {
            return commentsService
              .postComment(
                store.finding()!.id,
                store.composers()[_composerKey].draft,
                _composerKey === TOP_COMPOSER_KEY ? null : _composerKey,
              )
              .pipe(
                tapResponse({
                  next: (commentDto) => {
                    cancelReplyComposer(_composerKey);

                    const finding = store.finding()!;
                    finding.commentCount++;
                    let comments: CommentThreadDto[] | null;
                    if (_composerKey === TOP_COMPOSER_KEY) {
                      comments = [{ ...commentDto, replies: [] }, ...(store.comments() || [])];
                    } else {
                      comments = [...store.comments()!];
                      const threadIdx = comments.findIndex((t) => t.id === _composerKey);
                      comments[threadIdx].replies = [...comments[threadIdx].replies, commentDto];
                    }
                    patchState(store, { comments, finding });
                  },
                  error: () => {
                    const { [_composerKey]: _notPosted, ...rest } = store.composers();
                    patchState(store, {
                      composers: { ...rest, [_composerKey]: { ..._notPosted, pending: false } },
                    });
                    snackBar.open('Some issues occured while posting a comment. Try again.');
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
        openReplyComposer,
        updateComposerDraft,
        cancelReplyComposer,
        postComment,
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
