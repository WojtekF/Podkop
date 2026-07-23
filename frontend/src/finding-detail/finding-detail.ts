import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, UrlSegment, RouterLink } from '@angular/router';
import { FindingDetailStore } from './finding-detail.store';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';

@Component({
  selector: 'app-finding-detail',
  imports: [MatProgressSpinnerModule, MatButton, RouterLink, RouterLink],
  providers: [FindingDetailStore],
  templateUrl: './finding-detail.html',
  styleUrl: './finding-detail.scss',
})
export class FindingDetail {
  protected readonly store = inject(FindingDetailStore);
  protected readonly route = inject(ActivatedRoute);

  constructor() {
    // The URL is the source of truth: read the :id route parameter and ask the store to
    // load that finding whenever it changes. Left unimplemented — finding-detail.spec.ts
    // specifies the behaviour to satisfy.
    this.route.url.pipe(takeUntilDestroyed()).subscribe((value: UrlSegment[]) => {
      this.store.load(value[1].path);
    });
  }

  protected retry(): void {
    this.store.retry();
  }
}
