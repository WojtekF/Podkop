import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

export type FindingVoteSide = 'dig' | 'bury';

export type BuryReason =
  'duplicate' | 'spam' | 'false-information' | 'inappropriate-content' | 'unsuitable';

export type FindingVoteIntent = { type: 'dig' } | { type: 'bury'; reason: BuryReason };

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
  myVote: FindingVoteSide | null;
  commentCount: number;
  createdAt: string;
  promotedAt: string | null;
}

export interface FindingVotesDto {
  digCount: number;
  myVote: FindingVoteSide | null;
}

@Injectable({
  providedIn: 'root',
})
export class FindingDetailService {
  private readonly http = inject(HttpClient);

  getFinding(id: string): Observable<FindingDetailDto> {
    return this.http.get<FindingDetailDto>(`/api/findings/${id}`).pipe(timeout(5000));
  }

  setMyVote(id: string, intent: FindingVoteIntent): Observable<FindingVotesDto> {
    return this.http
      .put<FindingVotesDto>(`/api/findings/${id}/my-vote`, intent)
      .pipe(timeout(5000));
  }

  withdrawMyVote(id: string): Observable<FindingVotesDto> {
    return this.http.delete<FindingVotesDto>(`/api/findings/${id}/my-vote`).pipe(timeout(5000));
  }
}
