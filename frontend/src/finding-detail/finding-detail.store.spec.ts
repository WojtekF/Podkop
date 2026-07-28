import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { commentThreads, findingDetail as detail, findingId as id } from './finding-detail.fixtures';
import { FindingDetailStore } from './finding-detail.store';

describe('FindingDetailStore', () => {
  let store: InstanceType<typeof FindingDetailStore>;
  let httpMock: HttpTestingController;
  let snackBar: { open: ReturnType<typeof vi.fn> };

  const otherId = '0d4f9a3e-2222-4222-8333-444455556666';

  const expectDetailRequest = (findingId: string) => httpMock.expectOne(`/api/findings/${findingId}`);
  const expectCommentsRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}/comments`);

  beforeEach(() => {
    snackBar = { open: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        FindingDetailStore,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
      ],
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

  describe('voting on a comment (issue #18)', () => {
    // From the fixtures: grace's thread is voted up, linus's reply is voted down,
    // margaret's thread has no vote yet.
    const votedUpThread = () => commentThreads()[0];
    const votedDownReply = () => commentThreads()[0].replies[0];
    const freshThread = () => commentThreads()[1];

    const expectVoteRequest = (commentId: string) =>
      httpMock.expectOne(`/api/comments/${commentId}/my-vote`);

    const loadDiscussion = () => {
      store.load(id);
      expectDetailRequest(id).flush(detail());
      expectCommentsRequest(id).flush(commentThreads());
    };

    it('voting on a comment without a vote PUTs the chosen direction', () => {
      loadDiscussion();

      store.voteOnComment(freshThread().id, 'up');

      const req = expectVoteRequest(freshThread().id);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ direction: 'up' });
    });

    it('the response reconciles exactly that comment in place — no refetch', () => {
      loadDiscussion();

      store.voteOnComment(freshThread().id, 'up');
      expectVoteRequest(freshThread().id).flush({ upvoteCount: 4, downvoteCount: 4, myVote: 'up' });

      expect(store.comments()?.[1]).toEqual({
        ...freshThread(),
        upvoteCount: 4,
        downvoteCount: 4,
        myVote: 'up',
      });
      expect(store.comments()?.[0]).toEqual(votedUpThread());
      httpMock.expectNone(`/api/findings/${id}/comments`);
    });

    it('clicking the side already held withdraws the vote with a DELETE', () => {
      loadDiscussion();

      store.voteOnComment(votedUpThread().id, 'up');

      const req = expectVoteRequest(votedUpThread().id);
      expect(req.request.method).toBe('DELETE');

      req.flush({ upvoteCount: 11, downvoteCount: 2, myVote: null });
      expect(store.comments()?.[0].upvoteCount).toBe(11);
      expect(store.comments()?.[0].myVote).toBeNull();
    });

    it('clicking the other side switches with a single PUT', () => {
      loadDiscussion();

      store.voteOnComment(votedUpThread().id, 'down');

      const req = expectVoteRequest(votedUpThread().id);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ direction: 'down' });
    });

    it('a vote on a reply reconciles inside its thread', () => {
      loadDiscussion();

      // The reply currently holds a down vote, so choosing up is a switch.
      store.voteOnComment(votedDownReply().id, 'up');
      expectVoteRequest(votedDownReply().id).flush({
        upvoteCount: 2,
        downvoteCount: 0,
        myVote: 'up',
      });

      expect(store.comments()?.[0].replies[0]).toEqual({
        ...votedDownReply(),
        upvoteCount: 2,
        downvoteCount: 0,
        myVote: 'up',
      });
    });

    it('marks the comment pending while its request is in flight, and only then', () => {
      loadDiscussion();

      store.voteOnComment(freshThread().id, 'up');
      expect(store.pendingCommentVoteIds()).toContain(freshThread().id);

      expectVoteRequest(freshThread().id).flush({ upvoteCount: 4, downvoteCount: 4, myVote: 'up' });
      expect(store.pendingCommentVoteIds()).toEqual([]);
    });

    it('a failed vote announces itself in a snackbar and leaves the discussion untouched', () => {
      loadDiscussion();

      store.voteOnComment(freshThread().id, 'up');
      expectVoteRequest(freshThread().id).flush('boom', {
        status: 500,
        statusText: 'Server Error',
      });

      expect(store.comments()).toEqual(commentThreads());
      expect(store.pendingCommentVoteIds()).toEqual([]);
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain("Couldn't record your vote");
    });
  });
});
