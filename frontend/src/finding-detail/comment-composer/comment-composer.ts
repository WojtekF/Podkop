import { Component, computed, input, output } from '@angular/core';

/** The most text one comment may carry (issue #17); mirrors the backend's Comment.MaxTextLength. */
export const COMMENT_MAX_LENGTH = 5000;

/**
 * The comment composer (issue #17), shared between the top-level usage (always visible at the
 * top of the comments section) and the inline reply usage (opened under a thread). The draft is
 * owned by the store and flows in through `draft`; edits flow out through `draftChange`.
 */
@Component({
  selector: 'app-comment-composer',
  imports: [],
  templateUrl: './comment-composer.html',
  styleUrl: './comment-composer.scss',
})
export class CommentComposer {
  readonly draft = input<string>('');
  readonly pending = input<boolean>(false);
  // Only the inline reply usage offers a cancel; the top-level composer is permanent.
  readonly cancellable = input<boolean>(false);
  readonly draftChange = output<string>();
  readonly post = output<void>();
  readonly cancel = output<void>();

  protected readonly maxLength = COMMENT_MAX_LENGTH;
  protected readonly isOverLimit = computed(() => this.draft().length > COMMENT_MAX_LENGTH);
  protected readonly isSubmitDisabled = computed(
    () => this.pending() || this.isOverLimit() || this.draft().trim().length === 0,
  );
}
