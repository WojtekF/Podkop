import { CanActivateFn } from '@angular/router';

/**
 * Gate on the moderator area (issue #34): resolves once the app-wide who-am-I state
 * (CurrentUserStore) has answered — waiting out a still-loading fetch — then admits a
 * Moderator and turns everyone else around to the main page: Members, and an acting user the
 * app could not load. The API refuses non-moderators on its own; this guard only spares them
 * the refused page, and the shell shows them no way in to begin with.
 */
export const moderatorGuard: CanActivateFn = () => {
  throw new Error('not implemented');
};
