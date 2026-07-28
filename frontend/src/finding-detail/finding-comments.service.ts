import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

export interface CommentDto {
  id: string;
  author: string;
  text: string;
  upvoteCount: number;
  downvoteCount: number;
  createdAt: string;
}

export interface CommentThreadDto extends CommentDto {
  replies: CommentDto[];
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
}
