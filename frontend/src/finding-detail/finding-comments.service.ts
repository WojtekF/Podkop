import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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

  // Fetch the whole discussion under a finding from the comments endpoint: top-level threads
  // best-first exactly as the server sent them (the frontend never re-sorts), each carrying
  // its replies in chronological order. An unknown finding must surface as a 404 the caller
  // can distinguish; every other failure is a plain load error. Mirror the detail service's
  // client-side timeout. See finding-comments.service.spec.ts for the request shape and
  // timeout behaviour.
  getComments(findingId: string): Observable<CommentThreadDto[]> {
    throw new Error('not implemented');
  }
}
