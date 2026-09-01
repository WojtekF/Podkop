import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TagPageStore } from './tag-page.store';
import { card, contentId, ref, tagPage } from './tags.fixtures';

describe('TagPageStore', () => {
  let store: InstanceType<typeof TagPageStore>;
  let httpMock: HttpTestingController;

  const expectTagRequest = (page: number) =>
    httpMock.expectOne((r) => {
      const params = new URL(r.urlWithParams, 'http://test').searchParams;
      return r.url.startsWith('/api/tags/') && params.get('page') === String(page);
    });

  const expectBatchRequest = () =>
    httpMock.expectOne((r) => r.url.startsWith('/api/findings/batch'));

  const ids = () => store.items().map((item) => item.finding.id);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TagPageStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(TagPageStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('starts loading page 1 of the combined stream with no items', () => {
    expect(store.status()).toBe('loading');
    expect(store.page()).toBe(1);
    expect(store.filter()).toBe('all');
    expect(store.items()).toEqual([]);
  });

  it('load fetches the references first and hydrates them second', () => {
    // The two calls cannot be parallel: the batch's ids are what the first call returned.
    store.load('dotnet', 'all', 1);

    expectTagRequest(1).flush(tagPage([ref(1), ref(2)]));
    httpMock.expectNone((r) => r.url.startsWith('/api/findings/batch'));
    expect(store.status()).toBe('loading');

    expectBatchRequest().flush([card(1), card(2)]);

    expect(store.status()).toBe('loaded');
    expect(ids()).toEqual([contentId(1), contentId(2)]);
  });

  it('renders the stream in the index’s order, whatever order hydration answered in', () => {
    // The server decided Newest; the frontend never re-sorts, and a batch endpoint promises no
    // ordering of its own.
    store.load('dotnet', 'all', 1);
    expectTagRequest(1).flush(tagPage([ref(3), ref(1), ref(2)]));

    expectBatchRequest().flush([card(1), card(2), card(3)]);

    expect(ids()).toEqual([contentId(3), contentId(1), contentId(2)]);
  });

  it('drops references that hydrated to nothing', () => {
    // ADR 0011: content that has just vanished hydrates to nothing and the page renders short,
    // rather than showing a hole or failing.
    store.load('dotnet', 'all', 1);
    expectTagRequest(1).flush(tagPage([ref(1), ref(2), ref(3)]));

    expectBatchRequest().flush([card(1), card(3)]);

    expect(store.status()).toBe('loaded');
    expect(ids()).toEqual([contentId(1), contentId(3)]);
  });

  it('a page whose references all hydrate to nothing is loaded and empty, not an error', () => {
    store.load('dotnet', 'all', 1);
    expectTagRequest(1).flush(tagPage([ref(1)]));

    expectBatchRequest().flush([]);

    expect(store.status()).toBe('loaded');
    expect(store.items()).toEqual([]);
  });

  it('an empty page of references needs no hydration call and is loaded and empty', () => {
    store.load('dotnet', 'entries', 1);

    expectTagRequest(1).flush(tagPage([]));

    httpMock.expectNone((r) => r.url.startsWith('/api/findings/batch'));
    expect(store.status()).toBe('loaded');
    expect(store.items()).toEqual([]);
  });

  it('hydrates only the ids the page actually named', () => {
    store.load('dotnet', 'all', 1);
    expectTagRequest(1).flush(tagPage([ref(2), ref(5)]));

    const batch = expectBatchRequest();

    const named = new URL(batch.request.urlWithParams, 'http://test').searchParams.get('ids');
    expect(named?.split(',').sort()).toEqual([contentId(2), contentId(5)].sort());
    batch.flush([card(2), card(5)]);
  });

  it('carries the tag, the filter, the page, and the next-page signal it loaded', () => {
    store.load('dotnet', 'findings', 2);
    expectTagRequest(2).flush(tagPage([ref(1)], true));
    expectBatchRequest().flush([card(1)]);

    expect(store.name()).toBe('dotnet');
    expect(store.filter()).toBe('findings');
    expect(store.page()).toBe(2);
    expect(store.hasNextPage()).toBe(true);
  });

  it('load replaces the previous page instead of appending to it', () => {
    store.load('dotnet', 'all', 1);
    expectTagRequest(1).flush(tagPage([ref(1), ref(2)], true));
    expectBatchRequest().flush([card(1), card(2)]);

    store.load('dotnet', 'all', 2);
    expectTagRequest(2).flush(tagPage([ref(3)]));
    expectBatchRequest().flush([card(3)]);

    expect(ids()).toEqual([contentId(3)]);
    expect(store.hasNextPage()).toBe(false);
  });

  it('a 404 on the tag puts the store in the not-found state, distinct from a load error', () => {
    store.load('nosuchtag', 'all', 1);

    expectTagRequest(1).flush('missing', { status: 404, statusText: 'Not Found' });

    expect(store.status()).toBe('notFound');
    expect(store.items()).toEqual([]);
  });

  it('any other failure on the tag call is an error state', () => {
    store.load('dotnet', 'all', 1);

    expectTagRequest(1).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
  });

  it('a failed hydration is an error state, not a half-rendered page', () => {
    // A page of cards that cannot be drawn is not a page the reader can use — and it is not a
    // missing tag either: the tag answered.
    store.load('dotnet', 'all', 1);
    expectTagRequest(1).flush(tagPage([ref(1)]));

    expectBatchRequest().flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
    expect(store.items()).toEqual([]);
  });

  it('retry reloads the tag, filter, and page the store is showing', () => {
    store.load('dotnet', 'findings', 2);
    expectTagRequest(2).flush('boom', { status: 500, statusText: 'Server Error' });

    store.retry();

    const retried = expectTagRequest(2);
    const params = new URL(retried.request.urlWithParams, 'http://test').searchParams;
    expect(retried.request.url).toBe('/api/tags/dotnet');
    expect(params.get('type')).toBe('findings');
    expect(store.status()).toBe('loading');
    retried.flush(tagPage([]));
  });
});
