import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import { FindingDetail, FindingDetailService } from './finding-detail.service';

describe('FindingDetailService', () => {
  let service: FindingDetailService;
  let httpMock: HttpTestingController;

  const id = '0d4f9a3e-1111-4222-8333-444455556666';

  const detail = (): FindingDetail => ({
    id,
    title: 'A remarkable finding',
    description: 'The full, untruncated description.',
    sourceUrl: 'https://blog.example.org/posts/42',
    domain: 'blog.example.org',
    thumbnailUrl: 'https://example.com/thumb.jpg',
    author: 'ada_lovelace',
    tags: ['angular', 'webdev'],
    digCount: 123,
    commentCount: 9,
    createdAt: '2026-07-08T03:30:00Z',
    promotedAt: '2026-07-08T09:30:00Z',
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(FindingDetailService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the finding by id from the detail endpoint', () => {
    let received: FindingDetail | undefined;
    service.getFinding(id).subscribe((finding) => (received = finding));

    const req = httpMock.expectOne(`/api/findings/${id}`);
    expect(req.request.method).toBe('GET');

    const body = detail();
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('propagates a 404 as an error the caller can inspect', () => {
    let status: number | undefined;
    service.getFinding(id).subscribe({
      error: (e: { status?: number }) => (status = e.status),
    });

    httpMock
      .expectOne(`/api/findings/${id}`)
      .flush('missing', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let error: unknown;
      service.getFinding(id).subscribe({ error: (e) => (error = e) });

      const req = httpMock.expectOne(`/api/findings/${id}`);
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
