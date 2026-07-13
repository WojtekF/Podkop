import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FindingSummary } from '../main-page-feed.service';

@Component({
  selector: 'main-page-finding-card',
  imports: [MatCardModule, MatButtonModule, MatIconModule, DatePipe],
  templateUrl: './finding-card.html',
  styleUrl: './finding-card.scss',
})
export class FindingCard {
  finding = input.required<FindingSummary>();

  /** Inert on the Main Page — the Votes feature wires it up later. */
  dig = output<void>();

  /** Drives the thumbnail/placeholder swap in the template. */
  protected hasThumbnail(): boolean {
    throw new Error('not implemented');
  }

  protected onDig(): void {
    throw new Error('not implemented');
  }
}
