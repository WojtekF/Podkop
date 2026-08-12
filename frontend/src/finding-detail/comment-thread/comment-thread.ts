import { Component, input, output } from '@angular/core';
import { CommentThreadDto, CommentVoteDirection } from '../finding-comments.service';
import { CommentRow } from '../comment-row/comment-row';
import { MatCard } from '@angular/material/card';
import { ComposerState } from '../finding-detail.store';
import { CommentComposer } from '../comment-composer/comment-composer';

// A vote clicked somewhere in a thread, tagged with the comment it belongs to.
export interface CommentVote {
  commentId: string;
  direction: CommentVoteDirection;
}

// A reply request bubbling up from a row (issue #17): every reply targets this thread's
// top-level comment — threads are one level deep — and when the answered comment was itself
// a reply, its author's @name travels along to be appended to the composer draft.
export interface ReplyRequest {
  threadId: string;
  appendAuthor: string | null;
}

@Component({
  selector: 'app-comment-thread',
  imports: [CommentRow, MatCard, CommentComposer],
  templateUrl: './comment-thread.html',
  styleUrl: './comment-thread.scss',
})
export class CommentThread {
  readonly thread = input.required<CommentThreadDto>();
  // Ids of comments whose vote request is in flight, straight from the store.
  readonly pendingVoteIds = input<readonly string[]>([]);
  // Ids of comments the current user already reported (issue #33), straight from the store's
  // batch my-reports state.
  readonly reportedCommentIds = input<readonly string[]>([]);
  // This thread's reply composer, straight from the store — null while closed (issue #17).
  readonly composer = input<ComposerState | undefined>(undefined);
  readonly vote = output<CommentVote>();
  readonly reply = output<ReplyRequest>();
  // A report request bubbling up from a row (issue #33), tagged with the comment it targets —
  // a reply names itself, not its thread.
  readonly report = output<string>();
  readonly composerDraftChange = output<string>();
  readonly composerPost = output<void>();
  readonly composerCancel = output<void>();
}
