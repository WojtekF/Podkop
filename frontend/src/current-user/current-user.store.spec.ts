import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { myUser } from './current-user.fixtures';
import { CurrentUserStore } from './current-user.store';

describe('CurrentUserStore', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  // The store is root-provided and self-loading: its first injection IS the trigger of the
  // one app-wide fetch, so every spec starts by injecting it.
  const injectStore = () => TestBed.inject(CurrentUserStore);

  const expectMyUserRequest = () => httpMock.expectOne({ method: 'GET', url: '/api/my-user' });

  it('injecting the store starts the one fetch and stays loading until it answers', () => {
    const store = injectStore();

    const request = expectMyUserRequest();
    expect(store.status()).toBe('loading');
    expect(store.user()).toBeNull();

    request.flush(myUser());
    expect(store.status()).toBe('loaded');
    expect(store.user()).toEqual(myUser());
  });

  it('a Member answer is exposed as-is', () => {
    const store = injectStore();

    expectMyUserRequest().flush(myUser({ userName: 'linus_t', role: 'Member' }));

    expect(store.user()).toEqual({ userName: 'linus_t', role: 'Member' });
    expect(store.status()).toBe('loaded');
  });

  it('a failed fetch parks the store in error with no user', () => {
    const store = injectStore();

    expectMyUserRequest().flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
    expect(store.user()).toBeNull();
  });

  it('later consumers reuse the answered fetch — injecting again requests nothing', () => {
    const first = injectStore();
    expectMyUserRequest().flush(myUser());

    const second = injectStore();

    expect(second).toBe(first);
    expect(second.user()).toEqual(myUser());
    httpMock.expectNone({ method: 'GET', url: '/api/my-user' });
  });
});
