import { Component, input } from '@angular/core';
import { CommentDto } from '../finding-comments.service';
import { TimeAgoPipe } from './time-ago.pipe';
import { MatCard, MatCardContent } from '@angular/material/card';

@Component({
  selector: 'app-comment-row',
  imports: [TimeAgoPipe, MatCard, MatCardContent],
  templateUrl: './comment-row.html',
  styleUrl: './comment-row.scss',
})
export class CommentRow {
  readonly isReply = input<boolean>(true);
  readonly comment = input.required<CommentDto>();
}
