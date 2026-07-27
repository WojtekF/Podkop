import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import { CommentThreadDto, FindingCommentsService } from './finding-comments.service';
import { commentThreads, findingId as id } from './finding-detail.fixtures';

describe('FindingCommentsService', () => {
  let service: FindingCommentsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(FindingCommentsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it("GETs the finding's discussion from the comments endpoint", () => {
    let received: CommentThreadDto[] | undefined;
    service.getComments(id).subscribe((threads) => (received = threads));

    const req = httpMock.expectOne(`/api/findings/${id}/comments`);
    expect(req.request.method).toBe('GET');

    const body = commentThreads();
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('propagates a 404 as an error the caller can inspect', () => {
    let status: number | undefined;
    service.getComments(id).subscribe({
      error: (e: { status?: number }) => (status = e.status),
    });

    httpMock
      .expectOne(`/api/findings/${id}/comments`)
      .flush('missing', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let error: unknown;
      service.getComments(id).subscribe({ error: (e) => (error = e) });

      const req = httpMock.expectOne(`/api/findings/${id}/comments`);
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
