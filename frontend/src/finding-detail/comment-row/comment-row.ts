import { Component, computed, input, output } from '@angular/core';
import { CommentDto, CommentVoteDirection } from '../finding-comments.service';
import { TimeAgoPipe } from './time-ago.pipe';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatIconButton, MatAnchor } from '@angular/material/button';
import { CURRENT_USER } from '../current-user';

@Component({
  selector: 'app-comment-row',
  imports: [TimeAgoPipe, MatCard, MatCardContent, MatIconButton, MatIcon, MatAnchor],
  templateUrl: './comment-row.html',
  styleUrl: './comment-row.scss',
})
export class CommentRow {
  readonly isReply = input<boolean>(true);
  readonly comment = input.required<CommentDto>();
  readonly votePending = input<boolean>(false);
  readonly vote = output<CommentVoteDirection>();
  // A request to answer this comment (issue #17). Live on every comment, own ones included —
  // replying to yourself is allowed; only voting is not.
  readonly reply = output<void>();
  protected readonly isOwnComment = computed(() => this.comment().author === CURRENT_USER);

  protected readonly isButtonDisabled = computed(() => this.votePending() || this.isOwnComment());
}
