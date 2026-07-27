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
  protected hasThumbnail() {
    return !!this.store.finding()?.thumbnailUrl;
  }
  protected readonly store = inject(FindingDetailStore);

  protected readonly id = input.required<string>();

  // Issue #16: when the page is entered through a card's comment-count link — the URL
  // carries the `comments` fragment — the first comment must end up centered in the
  // viewport once the discussion has rendered, and only then; a plain visit stays at the
  // top. See the centering specs in finding-detail.spec.ts.
  constructor() {
    effect(() => {
      this.store.load(this.id());
    });
  }
}
