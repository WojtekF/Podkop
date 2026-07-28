import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import {
  CommentThreadDto,
  CommentVotesDto,
  FindingCommentsService,
} from './finding-comments.service';
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

  describe('voting on a comment (issue #18)', () => {
    const commentId = 'c0000000-0000-4000-8000-000000000002';
    const voteUrl = `/api/comments/${commentId}/my-vote`;

    it('PUTs the chosen direction to the my-vote endpoint and yields the fresh vote state', () => {
      let received: CommentVotesDto | undefined;
      service.setMyVote(commentId, 'up').subscribe((votes) => (received = votes));

      const req = httpMock.expectOne(voteUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ direction: 'up' });

      req.flush({ upvoteCount: 4, downvoteCount: 4, myVote: 'up' });
      expect(received).toEqual({ upvoteCount: 4, downvoteCount: 4, myVote: 'up' });
    });

    it('DELETEs the my-vote endpoint to withdraw and yields the freed counts', () => {
      let received: CommentVotesDto | undefined;
      service.withdrawMyVote(commentId).subscribe((votes) => (received = votes));

      const req = httpMock.expectOne(voteUrl);
      expect(req.request.method).toBe('DELETE');

      req.flush({ upvoteCount: 3, downvoteCount: 4, myVote: null });
      expect(received).toEqual({ upvoteCount: 3, downvoteCount: 4, myVote: null });
    });

    it('propagates a rejected vote as an error the caller can inspect', () => {
      let status: number | undefined;
      service.setMyVote(commentId, 'down').subscribe({
        error: (e: { status?: number }) => (status = e.status),
      });

      httpMock.expectOne(voteUrl).flush('own comment', { status: 400, statusText: 'Bad Request' });

      expect(status).toBe(400);
    });

    it('fails and cancels a vote request that hangs past 5 seconds', () => {
      vi.useFakeTimers();
      try {
        let error: unknown;
        service.setMyVote(commentId, 'up').subscribe({ error: (e) => (error = e) });

        const req = httpMock.expectOne(voteUrl);
        vi.advanceTimersByTime(5000);
        expect(error).toBeInstanceOf(TimeoutError);
        expect(req.cancelled).toBe(true);
      } finally {
        vi.useRealTimers();
      }
    });
  });
});
