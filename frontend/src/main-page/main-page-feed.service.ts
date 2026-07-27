import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

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
  promotedAt: string;
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
