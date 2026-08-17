import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { caseQueue } from './moderation.fixtures';
import { CaseQueueStore } from './case-queue.store';

describe('CaseQueueStore', () => {
  let httpMock: HttpTestingController;
  let snackBar: { open: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    snackBar = { open: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        CaseQueueStore,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
      ],
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

  describe('dismissing a case (issue #35)', () => {
    // From the fixtures: the Finding case and the Comment case live on the same finding page
    // but are distinct targets, and the third case is another Finding — see
    // moderation.fixtures.ts for what that arrangement proves.
    const findingCase = () => caseQueue()[0];
    const commentCase = () => caseQueue()[1];
    const untouchedCase = () => caseQueue()[2];

    const dismissUrl = (targetKind: string, targetId: string) =>
      `/api/moderation/cases/${targetKind}/${targetId}/verdict`;
    const expectDismissRequest = (targetKind: string, targetId: string) =>
      httpMock.expectOne({ method: 'POST', url: dismissUrl(targetKind, targetId) });

    const loadQueue = () => {
      const store = injectStore();
      store.load();
      expectQueueRequest().flush(caseQueue());
      return store;
    };

    it('marks the case pending by its target key while the request is in flight, and only then', () => {
      const store = loadQueue();

      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });
      expect(store.pendingDismissKeys()).toEqual([`Finding:${findingCase().targetId}`]);

      expectDismissRequest('Finding', findingCase().targetId).flush(null, {
        status: 204,
        statusText: 'No Content',
      });
      expect(store.pendingDismissKeys()).toEqual([]);
    });

    it("a successful dismissal removes exactly that case — the same finding page's comment case stays", () => {
      const store = loadQueue();

      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });
      expectDismissRequest('Finding', findingCase().targetId).flush(null, {
        status: 204,
        statusText: 'No Content',
      });

      // The dismissed Finding and the surviving Comment case share a findingId — only a
      // removal keyed on the target takes exactly one card's worth of state.
      expect(store.cases()!.map((c) => c.targetId)).toEqual([
        commentCase().targetId,
        untouchedCase().targetId,
      ]);
    });

    it('the unknown-case refusal removes the case too, silently — another moderator got there first', () => {
      const store = loadQueue();

      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });
      expectDismissRequest('Finding', findingCase().targetId).flush(
        { type: 'podkop:problem:unknown-case' },
        { status: 404, statusText: 'Not Found' },
      );

      expect(store.cases()!.map((c) => c.targetId)).toEqual([
        commentCase().targetId,
        untouchedCase().targetId,
      ]);
      expect(store.pendingDismissKeys()).toEqual([]);
      expect(snackBar.open).not.toHaveBeenCalled();
    });

    it('any other failure keeps the case, clears the pending key, and announces itself', () => {
      const store = loadQueue();

      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });
      expectDismissRequest('Finding', findingCase().targetId).flush('boom', {
        status: 500,
        statusText: 'Server Error',
      });

      expect(store.cases()).toEqual(caseQueue());
      expect(store.pendingDismissKeys()).toEqual([]);
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain("Couldn't dismiss the case.");
    });

    it('a repeat dismiss for an in-flight case is dropped whole — one request, one pending key', () => {
      const store = loadQueue();

      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });
      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });

      const open = httpMock.match({
        method: 'POST',
        url: dismissUrl('Finding', findingCase().targetId),
      });
      expect(open.length).toBe(1);
      expect(store.pendingDismissKeys()).toEqual([`Finding:${findingCase().targetId}`]);

      open[0].flush(null, { status: 204, statusText: 'No Content' });
      // The dropped intent left nothing behind: no second request, no key still pending.
      expect(store.pendingDismissKeys()).toEqual([]);
      httpMock.expectNone({
        method: 'POST',
        url: dismissUrl('Finding', findingCase().targetId),
      });
    });

    it('two different cases dismiss in parallel, each tracked by its own key', () => {
      const store = loadQueue();

      store.dismiss({ targetKind: 'Finding', targetId: findingCase().targetId });
      store.dismiss({ targetKind: 'Comment', targetId: commentCase().targetId });

      expect(store.pendingDismissKeys()).toContain(`Finding:${findingCase().targetId}`);
      expect(store.pendingDismissKeys()).toContain(`Comment:${commentCase().targetId}`);

      // The later intent resolves first; its sibling stays pending and listed.
      expectDismissRequest('Comment', commentCase().targetId).flush(null, {
        status: 204,
        statusText: 'No Content',
      });
      expect(store.pendingDismissKeys()).toEqual([`Finding:${findingCase().targetId}`]);
      expect(store.cases()!.map((c) => c.targetId)).toEqual([
        findingCase().targetId,
        untouchedCase().targetId,
      ]);

      expectDismissRequest('Finding', findingCase().targetId).flush(null, {
        status: 204,
        statusText: 'No Content',
      });
      expect(store.pendingDismissKeys()).toEqual([]);
      expect(store.cases()!.map((c) => c.targetId)).toEqual([untouchedCase().targetId]);
    });
  });
});
