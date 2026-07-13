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

  getPage(page: number): Observable<FeedPage> {
    throw new Error('not implemented');
  }
}
