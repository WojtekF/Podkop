import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

// The two sides of a finding vote — Dig and Bury in the glossary, distinct from a comment's
// Upvote/Downvote (issue #15). These are the values `myVote` carries.
export type FindingVoteSide = 'dig' | 'bury';

// The justification every bury carries — the closed list of five reasons (CONTEXT.md). The
// reason is sent when burying; it is never returned (bury reasons stay private).
export type BuryReason =
  | 'duplicate'
  | 'spam'
  | 'false-information'
  | 'inappropriate-content'
  | 'unsuitable';

// The intent behind a set-my-vote: a dig (no reason) or a bury naming one of the five reasons.
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

// The finding's fresh vote state after a mutation, for the frontend to reconcile from — no
// refetch. Only the dig count is public: no bury count is ever returned (issue #15).
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

  // Idempotent set-my-vote covering fresh digs and buries and side switches alike (issue #15).
  setMyVote(id: string, intent: FindingVoteIntent): Observable<FindingVotesDto> {
    throw new Error('not implemented');
  }

  withdrawMyVote(id: string): Observable<FindingVotesDto> {
    throw new Error('not implemented');
  }
}
