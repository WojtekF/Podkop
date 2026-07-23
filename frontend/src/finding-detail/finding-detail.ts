import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FindingDetailStore } from './finding-detail.store';

@Component({
  selector: 'app-finding-detail',
  imports: [],
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
  }

  protected retry(): void {
    throw new Error('not implemented');
  }
}
