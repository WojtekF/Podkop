import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

export interface FindingDetailDto {
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

  getFinding(id: string): Observable<FindingDetailDto> {
    return this.http.get<FindingDetailDto>(`/api/findings/${id}`).pipe(timeout(5000));
  }
}
