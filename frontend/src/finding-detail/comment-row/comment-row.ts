import { Component, computed, input, output } from '@angular/core';
import { CommentDto, CommentVoteDirection } from '../finding-comments.service';
import { TimeAgoPipe } from './time-ago.pipe';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatButton, MatIconButton } from '@angular/material/button';
import { CURRENT_USER } from '../current-user';
import { MatMenu, MatMenuTrigger, MatMenuItem } from '@angular/material/menu';

@Component({
  selector: 'app-comment-row',
  imports: [
    TimeAgoPipe,
    MatCard,
    MatCardContent,
    MatIconButton,
    MatIcon,
    MatButton,
    MatMenu,
    MatMenuTrigger,
    MatMenuItem,
  ],
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
  // Whether the current user already reported this comment (issue #33), straight from the
  // store's batch my-reports state.
  readonly reportedByMe = input<boolean>(false);
  // A request to report this comment (issue #33). Never available on own comments —
  // self-reports are refused.
  readonly report = output<void>();
  protected readonly isOwnComment = computed(() => this.comment().author === CURRENT_USER);

  protected readonly isButtonDisabled = computed(() => this.votePending() || this.isOwnComment());

  protected isActionMenuVisible() {
    return !this.isOwnComment();
  }
}
