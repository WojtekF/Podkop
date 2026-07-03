import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MainPost } from '../sink.service';

@Component({
  selector: 'sink-post-card',
  imports: [MatCardModule, DatePipe],
  templateUrl: './post-card.html',
  styleUrl: './post-card.scss',
})
export class PostCard {
  mainPost = input.required<MainPost>();
}
