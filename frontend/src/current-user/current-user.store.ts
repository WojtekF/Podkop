import { inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { CurrentUserService, MyUserDto } from './current-user.service';
import { asResult, isLoadFailure } from '../shared/as-result';
import { pipe, switchMap, tap } from 'rxjs';
import { rxMethod } from '@ngrx/signals/rxjs-interop';

export type CurrentUserStatus = 'loading' | 'loaded' | 'error';

export interface CurrentUserState {
  user: MyUserDto | null;
  status: CurrentUserStatus;
}

const initialState: CurrentUserState = {
  user: null,
  status: 'loading',
};

/**
 * App-wide who-am-I state (issue #31): the one fetch of GET /api/my-user, started the moment
 * the store is first injected, exposed as signals that every later consumer (the role-gated
 * moderator area, an identity display) reads instead of fetching again. `user` holds the
 * answer once loaded; a failed fetch parks the store in 'error' with no user.
 */
export const CurrentUserStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, service = inject(CurrentUserService)) => {
    /** Starts the one app-wide fetch of the acting user through the CurrentUserService. */
    const load = rxMethod<void>(
      pipe(
        switchMap(() => {
          return asResult(service.getMyUser()).pipe(
            tap({
              next: (response) => {
                if (isLoadFailure(response)) {
                  patchState(store, { user: null, status: 'error' });
                } else {
                  patchState(store, { user: response, status: 'loaded' });
                }
              },
            }),
          );
        }),
      ),
    );

    return { load };
  }),
  withHooks({
    onInit({ load }) {
      load();
    },
  }),
);
