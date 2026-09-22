import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TagPageStore } from '../tag-page.store';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { combineLatest } from 'rxjs/internal/observable/combineLatest';
import { MatButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { FindingCard } from '../../main-page/finding-card/finding-card';

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

  constructor() {
    // The URL is the source of truth: read the :name route parameter and the page and type
    // query parameters, and ask the store to load that tag page whenever any of them changes.
    // A missing, malformed, or non-positive page is page 1; a missing or unrecognised type is
    // the combined stream. Left unimplemented — tag-page.spec.ts specifies the behaviour to
    // satisfy.

    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed())
      .subscribe(([params, query]) => {
        const name = params.get('name') as string;

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
    let page = null;
    if (this.store.page() - 1 !== 1) {
      page = this.store.page() - 1;
    }

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        page,
        type: this.store.filter(),
      },
    });
  }

  protected goToNextPage(): void {
    if (this.store.hasNextPage()) {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          page: this.store.page() + 1,
          type: this.store.filter(),
        },
      });
    }
  }

  protected retry(): void {
    this.store.retry();
  }
}

const TAG_CONTENT_FILTERS = ['all', 'findings', 'entries'] as const;
type TagContentFilter = (typeof TAG_CONTENT_FILTERS)[number];

function isTagContentFilter(value: unknown): value is TagContentFilter {
  return (TAG_CONTENT_FILTERS as readonly unknown[]).includes(value);
}
