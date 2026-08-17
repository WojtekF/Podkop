import { CanActivateFn, Router } from '@angular/router';
import { CurrentUserStore } from '../current-user/current-user.store';
import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';

/**
 * Gate on the moderator area (issue #34): resolves once the app-wide who-am-I state
 * (CurrentUserStore) has answered — waiting out a still-loading fetch — then admits a
 * Moderator and turns everyone else around to the main page: Members, and an acting user the
 * app could not load. The API refuses non-moderators on its own; this guard only spares them
 * the refused page, and the shell shows them no way in to begin with.
 */
export const moderatorGuard: CanActivateFn = () => {
  const store = inject(CurrentUserStore);
  const router = inject(Router);

  return toObservable(store.status).pipe(
    filter((status) => status !== 'loading'),
    take(1),
    map(() => (store.user()?.role === 'Moderator' ? true : router.parseUrl('/'))),
  );
};
