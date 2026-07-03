import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { PostCard } from './post-card/post-card';
import { SinkService, MainPost } from './sink.service';

@Component({
  selector: 'app-sink',
  templateUrl: './sink.component.html',
  styleUrls: ['./sink.component.css'],
  imports: [PostCard],
})
export class SinkComponent {
  private sinkService = inject(SinkService);

  list = toSignal(this.sinkService.getItems(), { initialValue: [] as MainPost[] });
}
