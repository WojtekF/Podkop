import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FindingDetailStore } from './finding-detail.store';
import { MatButton } from '@angular/material/button';

@Component({
  selector: 'app-finding-detail',
  imports: [MatProgressSpinnerModule, MatButton, RouterLink],
  providers: [FindingDetailStore],
  templateUrl: './finding-detail.html',
  styleUrl: './finding-detail.scss',
})
export class FindingDetail {
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
