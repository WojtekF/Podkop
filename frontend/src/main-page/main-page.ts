import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MainPageStore } from './main-page.store';

@Component({
  selector: 'app-main-page',
  // Add whatever your template uses, e.g.:
  //   FindingCard              from './finding-card/finding-card'
  //   MatButtonModule          from '@angular/material/button'
  //   MatProgressSpinnerModule from '@angular/material/progress-spinner'
  imports: [],
  providers: [MainPageStore],
  templateUrl: './main-page.html',
  styleUrl: './main-page.scss',
})
export class MainPage {
  protected readonly store = inject(MainPageStore);
  protected readonly route = inject(ActivatedRoute);
  protected readonly router = inject(Router);

  constructor() {
    // URL-driven fetching (issue #7): react to every ?page= change — including
    // the initial navigation — parse it (missing or invalid → page 1) and load
    // that page through the store, so landing on the route always fetches
    // fresh findings.
    //
    // Useful pieces: this.route.queryParamMap is an Observable that emits on
    // every query-param change; subscribe to it here with
    // takeUntilDestroyed() (from '@angular/core/rxjs-interop') so the
    // subscription dies with the component. Number.parseInt / Number.isInteger
    // help with validation.
  }

  /**
   * The pager buttons navigate via the router — never call the store directly;
   * the URL is the single source of truth and the queryParamMap subscription
   * above reacts to it. Page 1 keeps a clean URL: pass { page: null } in
   * queryParams to drop the param, e.g.
   *   this.router.navigate([], { relativeTo: this.route, queryParams: { page: ... } });
   */
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

  /** Re-asks the store for the current page after a failed fetch. */
  protected retry(): void {
    throw new Error('not implemented');
  }
}
