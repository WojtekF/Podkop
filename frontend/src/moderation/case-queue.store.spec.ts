import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { caseQueue } from './moderation.fixtures';
import { CaseQueueStore } from './case-queue.store';

describe('CaseQueueStore', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CaseQueueStore, provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  const injectStore = () => TestBed.inject(CaseQueueStore);

  const expectQueueRequest = () =>
    httpMock.expectOne({ method: 'GET', url: '/api/moderation/cases' });

  it('load starts the queue fetch and stays loading until it answers', () => {
    const store = injectStore();
    store.load();

    const request = expectQueueRequest();
    expect(store.status()).toBe('loading');
    expect(store.cases()).toBeNull();

    request.flush(caseQueue());
    expect(store.status()).toBe('loaded');
    expect(store.cases()).toEqual(caseQueue());
  });

  it('keeps the served order untouched — the server owns it', () => {
    const store = injectStore();
    store.load();

    expectQueueRequest().flush(caseQueue());

    // The fixture's order is deliberately not count-sorted, not newest-first, and not
    // alphabetical (see moderation.fixtures.ts) — equality here proves nothing re-sorted it.
    expect(store.cases()!.map((c) => c.targetId)).toEqual(caseQueue().map((c) => c.targetId));
  });

  it('a failed fetch parks the store in error with no cases', () => {
    const store = injectStore();
    store.load();

    expectQueueRequest().flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
    expect(store.cases()).toBeNull();
  });

  it('the moderators-only refusal is an error state too', () => {
    const store = injectStore();
    store.load();

    expectQueueRequest().flush(
      { type: 'podkop:problem:moderators-only' },
      { status: 403, statusText: 'Forbidden' },
    );

    expect(store.status()).toBe('error');
    expect(store.cases()).toBeNull();
  });
});
