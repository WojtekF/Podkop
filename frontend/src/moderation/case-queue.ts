import { Component, inject } from '@angular/core';
import { CaseQueueStore } from './case-queue.store';

@Component({
  selector: 'app-case-queue',
  imports: [],
  providers: [CaseQueueStore],
  templateUrl: './case-queue.html',
  styleUrl: './case-queue.scss',
})
export class CaseQueue {
  protected readonly store = inject(CaseQueueStore);
}
