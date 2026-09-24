import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

/**
 * The card data the Findings slice serves, wherever a finding is shown as a card: the Main Page
 * feed and the batch-by-ids hydration a tag page runs on (issue #77). One interface for both,
 * because it is literally the same card — which is what lets the tag page render FindingCard.
 *
 * `promotedAt` is nullable and `createdAt` always present because of that second caller: the Main
 * Page carries promoted findings only, but a tag page carries every finding that took the tag,
 * promoted or still upcoming, and an upcoming finding has no promotion time to show.
 */
export interface FindingSummaryDto {
  id: string;
  title: string;
  description: string;
  sourceUrl: string;
  domain: string;
  thumbnailUrl: string | null;
  author: string;
  tags: string[];
  digCount: number;
  commentCount: number;
  createdAt: string;
  promotedAt: string | null;
}

export interface FeedPageDto {
  items: FindingSummaryDto[];
  hasNextPage: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class MainPageFeedService {
  private readonly http = inject(HttpClient);

  getPage(page: number): Observable<FeedPageDto> {
    return this.http
      .get<FeedPageDto>(`/api/findings`, {
        params: {
          page,
          feed: 'main',
        },
      })
      .pipe(timeout(5000));
  }
}
