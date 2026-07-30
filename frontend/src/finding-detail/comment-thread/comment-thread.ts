import { Component, input, output } from '@angular/core';
import { CommentThreadDto, CommentVoteDirection } from '../finding-comments.service';
import { CommentRow } from '../comment-row/comment-row';
import { MatCard } from '@angular/material/card';

// A vote clicked somewhere in a thread, tagged with the comment it belongs to.
export interface CommentVote {
  commentId: string;
  direction: CommentVoteDirection;
}

@Component({
  selector: 'app-comment-thread',
  imports: [CommentRow, MatCard],
  templateUrl: './comment-thread.html',
  styleUrl: './comment-thread.scss',
})
export class CommentThread {
  readonly thread = input.required<CommentThreadDto>();
  // Ids of comments whose vote request is in flight, straight from the store.
  readonly pendingVoteIds = input<readonly string[]>([]);
  readonly vote = output<CommentVote>();
}
