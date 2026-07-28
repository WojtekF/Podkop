import { Component, input } from '@angular/core';
import { CommentThreadDto } from '../finding-comments.service';
import { CommentRow } from '../comment-row/comment-row';
import { MatCard, MatCardContent } from '@angular/material/card';

@Component({
  selector: 'app-comment-thread',
  imports: [CommentRow, MatCard, MatCardContent],
  templateUrl: './comment-thread.html',
  styleUrl: './comment-thread.scss',
})
export class CommentThread {
  readonly thread = input.required<CommentThreadDto>();
}
