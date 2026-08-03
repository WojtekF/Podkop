import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import {
  FindingDetailDto,
  FindingDetailService,
  FindingVotesDto,
} from './finding-detail.service';
import { findingDetail as detail, findingId as id } from './finding-detail.fixtures';

describe('FindingDetailService', () => {
  let service: FindingDetailService;
  let httpMock: HttpTestingController;

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
    let received: FindingDetailDto | undefined;
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

  describe('voting on the finding (issue #15)', () => {
    const voteEndpoint = `/api/findings/${id}/my-vote`;

    it('PUTs a dig to the finding my-vote endpoint', () => {
      let received: FindingVotesDto | undefined;
      service.setMyVote(id, { type: 'dig' }).subscribe((votes) => (received = votes));

      const req = httpMock.expectOne(voteEndpoint);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ type: 'dig' });

      const body: FindingVotesDto = { digCount: 124, myVote: 'dig' };
      req.flush(body);
      expect(received).toEqual(body);
    });

    it('PUTs a bury carrying the chosen reason', () => {
      service.setMyVote(id, { type: 'bury', reason: 'spam' }).subscribe();

      const req = httpMock.expectOne(voteEndpoint);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ type: 'bury', reason: 'spam' });
    });

    it('DELETEs to withdraw the vote', () => {
      let received: FindingVotesDto | undefined;
      service.withdrawMyVote(id).subscribe((votes) => (received = votes));

      const req = httpMock.expectOne(voteEndpoint);
      expect(req.request.method).toBe('DELETE');

      const body: FindingVotesDto = { digCount: 123, myVote: null };
      req.flush(body);
      expect(received).toEqual(body);
    });
  });
});
