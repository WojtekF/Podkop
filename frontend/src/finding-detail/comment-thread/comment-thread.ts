import { Component, input } from '@angular/core';
import { CommentThreadDto } from '../finding-comments.service';

@Component({
  selector: 'app-comment-thread',
  imports: [],
  templateUrl: './comment-thread.html',
  styleUrl: './comment-thread.scss',
})
export class CommentThread {
  readonly thread = input.required<CommentThreadDto>();
}
