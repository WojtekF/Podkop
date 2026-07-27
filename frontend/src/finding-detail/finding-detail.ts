import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FindingDetailStore } from './finding-detail.store';
import { MatButton } from '@angular/material/button';
import { DatePipe } from '@angular/common';
import { MatCard, MatCardContent } from '@angular/material/card';

@Component({
  selector: 'app-finding-detail',
  imports: [MatProgressSpinnerModule, MatButton, RouterLink, DatePipe, MatCard, MatCardContent],
  providers: [FindingDetailStore],
  templateUrl: './finding-detail.html',
  styleUrl: './finding-detail.scss',
})
export class FindingDetail {
  hasThumbnail() {
    return !!this.store.finding()?.thumbnailUrl;
  }
  protected readonly store = inject(FindingDetailStore);

  protected readonly id = input.required<string>();
  constructor() {
    effect(() => {
      this.store.load(this.id());
    });
  }

  protected retry(): void {
    this.store.retry();
  }
}
