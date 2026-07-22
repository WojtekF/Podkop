import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MainPageStore } from './main-page.store';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { FindingCard } from './finding-card/finding-card';

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
    // URL is the single source of truth

    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const rawPage = params.get('page');
      let page = 1;
      if (rawPage !== null) {
        page = Number.parseInt(rawPage, 10);
        if (!Number.isInteger(page) || page < 1) {
          page = 1;
        }
      }
      this.store.loadPage(page);
    });
  }

  protected goToPreviousPage(): void {
    if (this.store.hasPreviousPage()) {
      let page = null;
      if (this.store.page() - 1 !== 1) {
        page = this.store.page() - 1;
      }

      this.router.navigate(['/'], {
        queryParams: {
          page,
        },
      });
    }
  }

  protected goToNextPage(): void {
    if (this.store.hasNextPage()) {
      this.router.navigate(['/'], {
        queryParams: {
          page: this.store.page() + 1,
        },
      });
    }
  }

  protected goToFirstPage(): void {
    this.router.navigate([], {
      queryParams: {
        page: null,
      },
    });
  }

  protected retry(): void {
    this.store.retry();
  }
}
