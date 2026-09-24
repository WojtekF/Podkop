import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  provideHttpClientTesting,
  HttpTestingController,
  TestRequest,
} from '@angular/common/http/testing';
import { FindingSummaryDto } from '../main-page/main-page-feed.service';
import { TagHydrationService } from './tag-hydration.service';
import { card, contentId } from './tags.fixtures';

describe('TagHydrationService', () => {
  let service: TagHydrationService;
  let httpMock: HttpTestingController;

  const params = (req: TestRequest) => new URL(req.request.urlWithParams, 'http://test').searchParams;

  const expectBatchRequest = () =>
    httpMock.expectOne((r) => r.url.startsWith('/api/findings/batch'));

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TagHydrationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs one batch naming every id it was given', () => {
    // One call per page, not one per card (ADR 0011) — the whole point of the batch endpoint.
    let received: FindingSummaryDto[] | undefined;
    service
      .getFindingsByIds([contentId(1), contentId(2), contentId(3)])
      .subscribe((cards) => (received = cards));

    const req = expectBatchRequest();
    expect(req.request.method).toBe('GET');
    expect(params(req).get('ids')).toBe(
      [contentId(1), contentId(2), contentId(3)].join(','),
    );

    const body = [card(1), card(2), card(3)];
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('makes no request at all when there is nothing to hydrate', () => {
    // An empty page must not send a batch the server would answer with a 400.
    let received: FindingSummaryDto[] | undefined;
    service.getFindingsByIds([]).subscribe((cards) => (received = cards));

    httpMock.expectNone((r) => r.url.startsWith('/api/findings/batch'));
    expect(received).toEqual([]);
  });

  it('hands back exactly what the server answered, however short', () => {
    // Ids naming vanished content come back absent; putting the cards into the page's order is
    // the caller's job, so the service must not pad, reorder, or invent.
    let received: FindingSummaryDto[] | undefined;
    service.getFindingsByIds([contentId(1), contentId(2)]).subscribe((cards) => (received = cards));

    expectBatchRequest().flush([card(2)]);

    expect(received).toEqual([card(2)]);
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let errored = false;
      service.getFindingsByIds([contentId(1)]).subscribe({ error: () => (errored = true) });

      const req = expectBatchRequest();
      vi.advanceTimersByTime(4999);
      expect(req.cancelled).toBe(false);

      vi.advanceTimersByTime(1);
      expect(errored).toBe(true);
      expect(req.cancelled).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
