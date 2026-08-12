import { CURRENT_USER } from './current-user';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  Component,
  ElementRef,
  Injector,
  effect,
  inject,
  input,
  viewChildren,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FindingDetailStore, TOP_COMPOSER_KEY } from './finding-detail.store';
import { MatButton } from '@angular/material/button';
import { DatePipe } from '@angular/common';
import { MatCard, MatCardActions, MatCardContent } from '@angular/material/card';
import { CommentThread, CommentVote } from './comment-thread/comment-thread';
import { FindingVote } from './finding-vote/finding-vote';
import { BuryReason } from './finding-detail.service';
import { CommentComposer } from './comment-composer/comment-composer';
import { MatDialog } from '@angular/material/dialog';
import { ReportDialog } from './report-dialog/report-dialog';

@Component({
  selector: 'app-finding-detail',
  imports: [
    MatProgressSpinnerModule,
    MatButton,
    RouterLink,
    DatePipe,
    MatCard,
    MatCardContent,
    CommentThread,
    FindingVote,
    CommentComposer,
    MatCardActions,
  ],
  providers: [FindingDetailStore],
  templateUrl: './finding-detail.html',
  styleUrl: './finding-detail.scss',
})
export class FindingDetail {
  protected readonly TOP_COMPOSER_KEY = TOP_COMPOSER_KEY;

  protected onVoteOnComment($event: CommentVote) {
    this.store.voteOnComment($event);
  }

  protected onDigVoteOnFinding() {
    this.store.voteOnFinding({ type: 'dig' });
  }

  protected onBuryVoteOnFinding(reason?: BuryReason) {
    this.store.voteOnFinding({ type: 'bury', reason });
  }

  protected hasThumbnail() {
    return !!this.store.finding()?.thumbnailUrl;
  }

  private readonly dialog = inject(MatDialog);
  protected isReportButtonDisabled() {
    return this.store.finding()?.author === CURRENT_USER || this.store.myReport() === true;
  }

  /**
   * Opens the same report dialog for one comment of the discussion (issue #33): the dialog is
   * told it targets a comment, its filing intent goes to the store's fileCommentReport tagged
   * with this comment's id, its pending state mirrors the store's comment-report pending state,
   * and it closes once this comment is among my reported comments — on any other failure it
   * stays open, the member's choice and note intact. Cancel just closes it.
   */
  protected onReportComment(commentId: string): void {
    throw new Error('not implemented');
  }

  protected openReportDialog() {
    const dialogRef = this.dialog.open(ReportDialog, { exitAnimationDuration: 0 });

    const sync = effect(
      () => dialogRef.componentRef?.setInput('pending', this.store.reportPending()),
      { injector: this.injector },
    );

    const closeOnReported = effect(
      () => {
        if (this.store.myReport()) dialogRef.close();
      },
      { injector: this.injector },
    );

    dialogRef.componentInstance.cancel.subscribe(() => {
      dialogRef.close();
    });

    dialogRef.componentInstance.fileReport.subscribe((report) => {
      this.store.fileReport(report);
    });

    dialogRef.afterClosed().subscribe(() => {
      sync.destroy();
      closeOnReported.destroy();
    });
  }

  private readonly injector = inject(Injector);
  protected readonly store = inject(FindingDetailStore);

  protected readonly id = input.required<string>();

  private readonly route = inject(ActivatedRoute);
  private readonly threadElements = viewChildren(CommentThread, { read: ElementRef });
  private scrolledToCommentsFor: string | undefined;

  constructor() {
    effect(() => {
      this.store.load(this.id());
    });

    effect(() => {
      const threads = this.threadElements();
      if (
        this.scrolledToCommentsFor === this.id() ||
        this.route.snapshot.fragment !== 'comments' ||
        threads.length === 0
      ) {
        return;
      }
      this.scrolledToCommentsFor = this.id();
      threads[0].nativeElement.scrollIntoView({ block: 'center' });
    });
  }
}
