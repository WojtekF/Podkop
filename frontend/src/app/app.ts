import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { CurrentUserStore } from '../current-user/current-user.store';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  providers: [CurrentUserStore],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('podkop');
}
