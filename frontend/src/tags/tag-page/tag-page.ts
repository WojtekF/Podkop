import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TagPageStore } from '../tag-page.store';
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
  imports: [],
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
  }

  /** Navigates to this page under a different type filter, starting again at page 1. */
  protected selectFilter(filter: TagContentFilter): void {
    throw new Error('not implemented');
  }

  protected goToPreviousPage(): void {
    throw new Error('not implemented');
  }

  protected goToNextPage(): void {
    throw new Error('not implemented');
  }

  protected retry(): void {
    this.store.retry();
  }
}
