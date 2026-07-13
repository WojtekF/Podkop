import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FindingCard } from './finding-card/finding-card';
import { MainPageStore } from './main-page.store';

@Component({
  selector: 'app-main-page',
  imports: [FindingCard, MatButtonModule, MatProgressSpinnerModule],
  providers: [MainPageStore],
  templateUrl: './main-page.html',
  styleUrl: './main-page.scss',
})
export class MainPage {
  protected readonly store = inject(MainPageStore);
  protected readonly route = inject(ActivatedRoute);
  protected readonly router = inject(Router);

  constructor() {
    // URL-driven fetching (issue #7): every ?page= change — including the initial
    // navigation — is parsed (missing or invalid → page 1) and loaded through the
    // store, so landing on the route always fetches fresh findings. The user
    // implements this wiring; until then nothing is fetched.
  }

  /** Navigates to the previous page via the router; page 1 keeps a clean URL ({ page: null }). */
  protected goToPreviousPage(): void {
    throw new Error('not implemented');
  }

  protected goToNextPage(): void {
    throw new Error('not implemented');
  }

  /** Escape hatch for stale deep links that land past the end of the feed. */
  protected goToFirstPage(): void {
    throw new Error('not implemented');
  }

  protected retry(): void {
    throw new Error('not implemented');
  }
}
