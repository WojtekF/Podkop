import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { commentThreads, findingDetail as detail, findingId as id } from './finding-detail.fixtures';
import { FindingDetailStore } from './finding-detail.store';

describe('FindingDetailStore', () => {
  let store: InstanceType<typeof FindingDetailStore>;
  let httpMock: HttpTestingController;

  const otherId = '0d4f9a3e-2222-4222-8333-444455556666';

  const expectDetailRequest = (findingId: string) => httpMock.expectOne(`/api/findings/${findingId}`);
  const expectCommentsRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}/comments`);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FindingDetailStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(FindingDetailStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('starts loading with no finding and no discussion', () => {
    expect(store.status()).toBe('loading');
    expect(store.finding()).toBeNull();
    expect(store.comments()).toBeNull();
  });

  it('load requests the finding and its discussion in parallel', () => {
    store.load(id);

    expectDetailRequest(id);
    expectCommentsRequest(id);
  });

  it('stays loading until both answers are in — the finding alone is not enough', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expect(store.status()).toBe('loading');

    expectCommentsRequest(id).flush(commentThreads());
    expect(store.status()).toBe('loaded');
    expect(store.finding()).toEqual(detail());
    expect(store.comments()).toEqual(commentThreads());
  });

  it('stays loading until both answers are in — the discussion alone is not enough', () => {
    store.load(id);

    expectCommentsRequest(id).flush(commentThreads());
    expect(store.status()).toBe('loading');

    expectDetailRequest(id).flush(detail());
    expect(store.status()).toBe('loaded');
  });

  it('keeps the threads exactly as the server ordered them', () => {
    store.load(id);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());

    expect(store.comments()?.map((t) => t.id)).toEqual(commentThreads().map((t) => t.id));
    expect(store.comments()?.[0].replies.map((r) => r.id)).toEqual(
      commentThreads()[0].replies.map((r) => r.id),
    );
  });

  it('a 404 on the finding puts the store in the not-found state, distinct from a load error', () => {
    store.load(id);

    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });

    expect(store.status()).toBe('notFound');
    expect(store.finding()).toBeNull();
  });

  it('a 404 on the discussion alone is still not-found — the finding is gone either way', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });

    expect(store.status()).toBe('notFound');
  });

  it('a failing finding request puts the store in the error state', () => {
    store.load(id);

    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectCommentsRequest(id).flush(commentThreads());

    expect(store.status()).toBe('error');
  });

  it('a failing discussion request is a load error even when the finding arrived', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
  });

  it('a finding request that never answers times out into the error state', () => {
    vi.useFakeTimers();
    try {
      store.load(id);
      expectCommentsRequest(id).flush(commentThreads());

      vi.advanceTimersByTime(4999);
      expect(store.status()).toBe('loading');

      vi.advanceTimersByTime(1);
      expect(store.status()).toBe('error');
      expect(store.finding()).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('a discussion timeout is a load error even when the finding arrived', () => {
    vi.useFakeTimers();
    try {
      store.load(id);
      expectDetailRequest(id).flush(detail());

      vi.advanceTimersByTime(5000);
      expect(store.status()).toBe('error');
    } finally {
      vi.useRealTimers();
    }
  });

  it('retry re-requests both the finding and the discussion for the id that failed', () => {
    store.load(id);
    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectCommentsRequest(id).flush(commentThreads());

    store.retry();

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expect(store.status()).toBe('loaded');
    expect(store.finding()).toEqual(detail());
    expect(store.comments()).toEqual(commentThreads());
  });

  it('loading a different id replaces the finding and the discussion it holds', () => {
    store.load(id);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());

    store.load(otherId);
    expectDetailRequest(otherId).flush(detail({ id: otherId, title: 'Another finding' }));
    expectCommentsRequest(otherId).flush([]);

    expect(store.finding()?.id).toBe(otherId);
    expect(store.finding()?.title).toBe('Another finding');
    expect(store.comments()).toEqual([]);
  });
});
