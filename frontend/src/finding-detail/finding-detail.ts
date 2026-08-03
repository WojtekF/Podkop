import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Component, ElementRef, effect, inject, input, viewChildren } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FindingDetailStore } from './finding-detail.store';
import { MatButton } from '@angular/material/button';
import { DatePipe } from '@angular/common';
import { MatCard, MatCardContent } from '@angular/material/card';
import { CommentThread, CommentVote } from './comment-thread/comment-thread';
import { FindingVote } from './finding-vote/finding-vote';
import { BuryReason } from './finding-detail.service';

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
    FindingVote
  ],
  providers: [FindingDetailStore],
  templateUrl: './finding-detail.html',
  styleUrl: './finding-detail.scss',
})
export class FindingDetail {
  protected onVoteOnComment($event: CommentVote) {
    this.store.voteOnComment($event);
  }

  protected onDigVoteOnFinding(){
    this.store.voteOnFinding({type:'dig'})
  }

  protected onBuryVoteOnFinding(reason: BuryReason){
    this.store.voteOnFinding({type:'bury', reason})
  }

  protected hasThumbnail() {
    return !!this.store.finding()?.thumbnailUrl;
  }

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
