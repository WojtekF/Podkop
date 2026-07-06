import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MainPost } from '../sink.service';

@Component({
  selector: 'sink-post-card',
  imports: [MatCardModule, MatButtonModule, MatIconModule, DatePipe],
  templateUrl: './post-card.html',
  styleUrl: './post-card.scss',
})
export class PostCard {
  mainPost = input.required<MainPost>();

  upvote = output<void>();
  downvote = output<void>();
}
