import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FeedPageDto, FindingSummaryDto } from './main-page-feed.service';
import { MainPageStore } from './main-page.store';

describe('MainPageStore', () => {
  let store: InstanceType<typeof MainPageStore>;
  let httpMock: HttpTestingController;

  const summary = (title: string): FindingSummaryDto => ({
    id: crypto.randomUUID(),
    title,
    description: `${title} — description`,
    sourceUrl: 'https://example.com/articles/1',
    domain: 'example.com',
    thumbnailUrl: null,
    author: 'grace_hopper',
    tags: ['dotnet'],
    digCount: 100,
    commentCount: 10,
    promotedAt: '2026-07-08T10:00:00Z',
  });

  const pageOf = (titles: string[], hasNextPage: boolean): FeedPageDto => ({
    items: titles.map(summary),
    hasNextPage,
  });

  const expectFeedRequest = (page: number) =>
    httpMock.expectOne((r) => {
      const params = new URL(r.urlWithParams, 'http://test').searchParams;
      return r.url.startsWith('/api/findings') && params.get('page') === String(page);
    });

  const titles = () => store.items().map((i) => i.title);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MainPageStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(MainPageStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('starts loading page 1 with no items', () => {
    expect(store.status()).toBe('loading');
    expect(store.page()).toBe(1);
    expect(store.items()).toEqual([]);
  });

  it('loadPage fetches the requested page and exposes it as loaded', () => {
    store.loadPage(1);

    expectFeedRequest(1).flush(pageOf(['A', 'B'], true));

    expect(store.status()).toBe('loaded');
    expect(titles()).toEqual(['A', 'B']);
    expect(store.page()).toBe(1);
    expect(store.hasNextPage()).toBe(true);
  });

  it('loadPage replaces the previous page instead of appending to it', () => {
    store.loadPage(1);
    expectFeedRequest(1).flush(pageOf(['A', 'B'], true));

    store.loadPage(2);
    expectFeedRequest(2).flush(pageOf(['C'], false));

    expect(titles()).toEqual(['C']);
    expect(store.page()).toBe(2);
    expect(store.hasNextPage()).toBe(false);
  });

  it('loadPage cancels an in-flight request when a new page is asked for', () => {
    store.loadPage(1);
    const first = expectFeedRequest(1);

    store.loadPage(2);

    expect(first.cancelled).toBe(true);
    expectFeedRequest(2).flush(pageOf(['C'], false));
    expect(titles()).toEqual(['C']);
  });

  it('a failed fetch puts the store in the error state', () => {
    store.loadPage(1);

    expectFeedRequest(1).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
  });

  it('retry re-requests the page that failed', () => {
    store.loadPage(2);
    expectFeedRequest(2).flush('boom', { status: 500, statusText: 'Server Error' });

    store.retry();

    expectFeedRequest(2).flush(pageOf(['C'], false));
    expect(store.status()).toBe('loaded');
    expect(titles()).toEqual(['C']);
  });

  it('hasPreviousPage is false on page 1 and true beyond it', () => {
    store.loadPage(1);
    expectFeedRequest(1).flush(pageOf(['A'], true));
    expect(store.hasPreviousPage()).toBe(false);

    store.loadPage(2);
    expectFeedRequest(2).flush(pageOf(['B'], false));
    expect(store.hasPreviousPage()).toBe(true);
  });

  it('isEmpty only once a loaded page has no items', () => {
    expect(store.isEmpty()).toBe(false); // still loading — not empty yet

    store.loadPage(1);
    expectFeedRequest(1).flush(pageOf([], false));

    expect(store.isEmpty()).toBe(true);
  });

  it('isEmpty stays false for a loaded page with items', () => {
    store.loadPage(1);
    expectFeedRequest(1).flush(pageOf(['A'], false));

    expect(store.isEmpty()).toBe(false);
  });
});
