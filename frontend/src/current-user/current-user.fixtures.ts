import { MyUserDto } from './current-user.service';

// Shared test data for the current-user specs (store, service).

// The acting user as the shipped backend seed answers it (issue #31): ada_lovelace, a
// Moderator. The Member shape is a spec's override away.
export const myUser = (overrides: Partial<MyUserDto> = {}): MyUserDto => ({
  userName: 'ada_lovelace',
  role: 'Moderator',
  ...overrides,
});
