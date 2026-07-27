import { Component, input } from '@angular/core';
import { CommentDto } from '../finding-comments.service';

@Component({
  selector: 'app-comment-row',
  imports: [],
  templateUrl: './comment-row.html',
  styleUrl: './comment-row.scss',
})
export class CommentRow {
  readonly comment = input.required<CommentDto>();
}
