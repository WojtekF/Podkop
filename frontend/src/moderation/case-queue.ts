import { Component, inject } from '@angular/core';
import { CaseQueueStore } from './case-queue.store';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import {
  MatCard,
  MatCardHeader,
  MatCardTitleGroup,
  MatCardTitle,
  MatCardSubtitle,
  MatCardContent,
} from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-case-queue',
  imports: [
    MatProgressSpinner,
    MatCard,
    MatCardHeader,
    MatCardTitleGroup,
    MatCardTitle,
    RouterLink,
    MatCardSubtitle,
    MatCardContent,
    DatePipe,
  ],
  providers: [CaseQueueStore],
  templateUrl: './case-queue.html',
  styleUrl: './case-queue.scss',
})
export class CaseQueue {
  protected readonly store = inject(CaseQueueStore);

  constructor() {
    this.store.load();
  }
}
