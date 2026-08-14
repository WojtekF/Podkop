import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type UserRole = 'Member' | 'Moderator';

// The acting user's identity and role as /api/my-user answers it (issue #31).
export interface MyUserDto {
  userName: string;
  role: UserRole;
}

@Injectable({
  providedIn: 'root',
})
export class CurrentUserService {
  private readonly http = inject(HttpClient);

  getMyUser(): Observable<MyUserDto> {
    throw new Error('not implemented');
  }
}
