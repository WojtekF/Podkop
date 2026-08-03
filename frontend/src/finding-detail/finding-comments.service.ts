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
  myVote: CommentVoteDirection | null;
  createdAt: string;
}

export interface CommentThreadDto extends CommentDto {
  replies: CommentDto[];
}

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

  setMyVote(commentId: string, direction: CommentVoteDirection): Observable<CommentVotesDto> {
    return this.http
      .put<CommentVotesDto>(`/api/comments/${commentId}/my-vote`, {
        direction,
      })
      .pipe(timeout(5000));
  }

  withdrawMyVote(commentId: string): Observable<CommentVotesDto> {
    return this.http
      .delete<CommentVotesDto>(`/api/comments/${commentId}/my-vote`)
      .pipe(timeout(5000));
  }

  /**
   * Posts a comment under the finding (issue #17): a top-level comment when parentCommentId is
   * null, a reply to that top-level comment otherwise. Returns the created comment in the same
   * shape a GET row has, for the store to render straight from the response — no refetch.
   */
  postComment(findingId: string, text: string, parentCommentId: string | null): Observable<CommentDto> {
    throw new Error('not implemented');
  }
}
