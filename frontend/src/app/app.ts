import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SinkComponent } from '../sink/sink.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SinkComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('podkop');
}
