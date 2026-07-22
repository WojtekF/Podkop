import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import { FeedPage, MainPageFeedService } from './main-page-feed.service';

describe('MainPageFeedService', () => {
  let service: MainPageFeedService;
  let httpMock: HttpTestingController;

  const feedParams = (req: TestRequest) =>
    new URL(req.request.urlWithParams, 'http://test').searchParams;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MainPageFeedService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the requested page of the main feed', () => {
    let received: FeedPage | undefined;
    service.getPage(3).subscribe((page) => (received = page));

    const req = httpMock.expectOne((r) => r.url.startsWith('/api/findings'));
    expect(req.request.method).toBe('GET');

    const params = feedParams(req);
    expect(params.get('feed')).toBe('main');
    expect(params.get('page')).toBe('3');

    const body: FeedPage = { items: [], hasNextPage: false };
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('always names the page, even for page 1', () => {
    service.getPage(1).subscribe();

    const req = httpMock.expectOne((r) => r.url.startsWith('/api/findings'));
    expect(feedParams(req).get('page')).toBe('1');
    req.flush({ items: [], hasNextPage: false });
  });

  it('does not send a limit — the server default applies', () => {
    service.getPage(1).subscribe();

    const req = httpMock.expectOne((r) => r.url.startsWith('/api/findings'));
    expect(feedParams(req).has('limit')).toBe(false);
    req.flush({ items: [], hasNextPage: false });
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let error: unknown;
      service.getPage(1).subscribe({ error: (e) => (error = e) });

      const req = httpMock.expectOne((r) => r.url.startsWith('/api/findings'));
      vi.advanceTimersByTime(4999);
      expect(req.cancelled).toBe(false);

      vi.advanceTimersByTime(1);
      expect(error).toBeInstanceOf(TimeoutError);
      expect(req.cancelled).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
