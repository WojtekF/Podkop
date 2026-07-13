import { Component, input, output } from '@angular/core';
import { FindingSummary } from '../main-page-feed.service';

@Component({
  selector: 'main-page-finding-card',
  // Add whatever your template uses, e.g.:
  //   DatePipe            from '@angular/common'
  //   MatCardModule       from '@angular/material/card'
  //   MatButtonModule     from '@angular/material/button'
  //   MatIconModule       from '@angular/material/icon'
  imports: [],
  templateUrl: './finding-card.html',
  styleUrl: './finding-card.scss',
})
export class FindingCard {
  finding = input.required<FindingSummary>();

  /** Inert on the Main Page — the Votes feature wires it up later. */
  dig = output<void>();

  /**
   * Drives the thumbnail/placeholder swap in the template:
   * true iff the finding has a thumbnailUrl.
   */
  protected hasThumbnail(): boolean {
    throw new Error('not implemented');
  }

  /** Click handler for the Dig button — should emit `dig` (this.dig.emit()). */
  protected onDig(): void {
    throw new Error('not implemented');
  }
}
