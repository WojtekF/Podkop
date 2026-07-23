import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FindingDetail {
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

@Injectable({
  providedIn: 'root',
})
export class FindingDetailService {
  private readonly http = inject(HttpClient);

  // Fetch one finding by id from the detail endpoint. A missing finding must surface as
  // a 404 the caller can distinguish (the store maps it to a "not found" state); every
  // other failure is a plain load error. Mirror the feed service's client-side timeout.
  // See finding-detail.service.spec.ts for the request shape and timeout behaviour.
  getFinding(id: string): Observable<FindingDetail> {
    throw new Error('not implemented');
  }
}
