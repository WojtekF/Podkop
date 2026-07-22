import { Component, input, output } from '@angular/core';
import { FindingSummary } from '../main-page-feed.service';
import { MatCardModule } from '@angular/material/card';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'main-page-finding-card',
  imports: [MatCardModule, DatePipe, MatButtonModule, MatIconModule],
  templateUrl: './finding-card.html',
  styleUrl: './finding-card.scss',
})
export class FindingCard {
  finding = input.required<FindingSummary>();

  dig = output<void>();

  protected hasThumbnail(): boolean {
    return this.finding().thumbnailUrl !== null;
  }

  protected onDig(): void {
    this.dig.emit();
  }
}
