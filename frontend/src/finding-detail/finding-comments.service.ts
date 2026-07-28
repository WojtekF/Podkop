import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

export type CommentVoteDirection = 'up' | 'down';

export interface CommentDto {
  id: string;
  author: string;
  text: string;
  upvoteCount: number;
  downvoteCount: number;
  // The current user's vote, straight from the server — this is what keeps highlighting
  // alive across a page reload (issue #18).
  myVote: CommentVoteDirection | null;
  createdAt: string;
}

export interface CommentThreadDto extends CommentDto {
  replies: CommentDto[];
}

// The fresh vote state of one comment after a mutation — the store reconciles from this
// response, it never refetches the discussion.
export interface CommentVotesDto {
  upvoteCount: number;
  downvoteCount: number;
  myVote: CommentVoteDirection | null;
}

@Injectable({
  providedIn: 'root',
})
export class FindingCommentsService {
  private readonly http = inject(HttpClient);

  getComments(findingId: string): Observable<CommentThreadDto[]> {
    return this.http
      .get<CommentThreadDto[]>(`/api/findings/${findingId}/comments`)
      .pipe(timeout(5000));
  }

  // Idempotent set-my-vote covering fresh votes and side switches alike (issue #18).
  setMyVote(commentId: string, direction: CommentVoteDirection): Observable<CommentVotesDto> {
    throw new Error('not implemented');
  }

  withdrawMyVote(commentId: string): Observable<CommentVotesDto> {
    throw new Error('not implemented');
  }
}
