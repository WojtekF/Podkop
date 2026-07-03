import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MainPost {
  id: number;
  title: string;
  content: string;
  image: string;
  createdAt: string;
  tags: string[];
  author: string;
  commentCount: number;
  upvoteCount: number;
  domain: string;
}

@Injectable({
  providedIn: 'root',
})
export class SinkService {
  private http = inject(HttpClient);

  private readonly apiUrl = '/api/sink';

  getItems(): Observable<MainPost[]> {
    return this.http.get<MainPost[]>(this.apiUrl);
  }
}
