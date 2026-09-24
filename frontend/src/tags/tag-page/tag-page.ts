import { Component, effect, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TagPageStore } from '../tag-page.store';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { FindingCard } from '../../main-page/finding-card/finding-card';
import { combineLatest } from 'rxjs';
import { TagContentFilter } from '../tags.service';

/**
 * The Tag Page (issue #77): one tag's combined stream at /tag/:name.
 *
 * The URL is the single source of truth, as on the Main Page: the tag comes from the route, the
 * page number from ?page= (ADR 0004 — Wykop's /strona/{n} path shape is presentation, not
 * behavior), and the type filter from ?type=. Changing the filter or turning a page navigates;
 * the store loads from whatever the URL then says.
 */
@Component({
  selector: 'app-tag-page',
  imports: [MatButton, MatProgressSpinner, RouterLink, FindingCard],
  providers: [TagPageStore],
  templateUrl: './tag-page.html',
  styleUrl: './tag-page.scss',
})
export class TagPage {
  protected readonly store = inject(TagPageStore);
  protected readonly route = inject(ActivatedRoute);
  protected readonly router = inject(Router);

  /**
   * The canonical name the URL is being rewritten to, while that rewrite is in flight. The
   * rewrite changes the route parameter, which would otherwise read as the reader going to
   * another tag and fetch the page a second time — the answer is already in hand.
   */
  private pendingCanonicalName: string | null = null;

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed())
      .subscribe(([params, query]) => {
        const name = params.get('name') as string;

        if (name === this.pendingCanonicalName) {
          this.pendingCanonicalName = null;
          return;
        }

        this.pendingCanonicalName = null;

        const rawType = query.get('type');
        const type: TagContentFilter = isTagContentFilter(rawType) ? rawType : 'all';

        const rawPage = query.get('page');
        let page = 1;
        if (rawPage !== null) {
          page = Number.parseInt(rawPage, 10);
          if (!Number.isInteger(page) || page < 1) {
            page = 1;
          }
        }

        this.store.load(name, type, page);
      });

    // Once the page resolves, the URL takes the canonical spelling the server answered with —
    // Wykop's /tag/POLSKA ends at /tag/polska. Replacing the history entry keeps Back from
    // returning the reader to the spelling that just bounced them.
    effect(() => {
      if (this.store.status() !== 'loaded') return;

      const canonicalName = this.store.name();
      const spelledName = this.route.snapshot.paramMap.get('name');
      if (canonicalName === null || spelledName === null || canonicalName === spelledName) return;

      this.pendingCanonicalName = canonicalName;
      this.router.navigate(['/tag', canonicalName], {
        queryParamsHandling: 'preserve',
        replaceUrl: true,
      });
    });
  }

  /** Navigates to this page under a different type filter, starting again at page 1. */
  protected selectFilter(filter: TagContentFilter): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        page: 1,
        type: filter,
      },
    });
  }

  protected goToPreviousPage(): void {
    let page = 1;
    if (this.store.page() - 1 !== 1) {
      page = this.store.page() - 1;
    }

    this.navigateToPageWithFilter({
      page,
      type: this.store.filter(),
    });
  }

  protected goToFirstPage(): void {
    this.navigateToPageWithFilter({
      type: this.store.filter(),
    });
  }

  protected goToNextPage(): void {
    if (this.store.hasNextPage()) {
      this.navigateToPageWithFilter({
        page: this.store.page() + 1,
        type: this.store.filter(),
      });
    }
  }

  private navigateToPageWithFilter(queryParams: Partial<{ page: number; type: TagContentFilter }>) {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
    });
  }

  protected retry(): void {
    this.store.retry();
  }
}

const TAG_CONTENT_FILTERS = ['all', 'findings', 'entries'] as const;

function isTagContentFilter(value: unknown): value is TagContentFilter {
  return (TAG_CONTENT_FILTERS as readonly unknown[]).includes(value);
}
