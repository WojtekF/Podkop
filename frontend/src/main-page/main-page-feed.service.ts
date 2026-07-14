import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FindingSummary {
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
  promotedAt: string;
}

export interface FeedPage {
  items: FindingSummary[];
  hasNextPage: boolean;
}

/**
 * Fetches pages of the Main Page feed from `GET /api/findings?feed=main&page=N`.
 * The page size is left to the server default — `limit` is deliberately not sent.
 */
@Injectable({
  providedIn: 'root',
})
export class MainPageFeedService {
  private readonly http = inject(HttpClient);

  /**
   * One GET per call; the specs check the query string, not how you build it —
   * `this.http.get<FeedPage>(url, { params: ... })` and a hand-built URL both
   * pass. Always send feed=main and page=N (page 1 included), never limit.
   */
  getPage(page: number): Observable<FeedPage> {
    return this.http.get<FeedPage>(`/api/findings`, {
      params: {
        page,
        feed: 'main',
      },
    });
  }
}
