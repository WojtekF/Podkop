import { Component, computed, input, output } from '@angular/core';
import { CommentDto, CommentVoteDirection } from '../finding-comments.service';
import { TimeAgoPipe } from './time-ago.pipe';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatIconButton } from '@angular/material/button';
import { CURRENT_USER } from '../current-user';

@Component({
  selector: 'app-comment-row',
  imports: [TimeAgoPipe, MatCard, MatCardContent, MatIconButton, MatIcon],
  templateUrl: './comment-row.html',
  styleUrl: './comment-row.scss',
})
export class CommentRow {
  readonly isReply = input<boolean>(true);
  readonly comment = input.required<CommentDto>();
  readonly votePending = input<boolean>(false);
  readonly vote = output<CommentVoteDirection>();
  protected readonly isOwnComment = computed(() => this.comment().author === CURRENT_USER);

  protected readonly isButtonDisabled = computed(() => this.votePending() || this.isOwnComment());
}
