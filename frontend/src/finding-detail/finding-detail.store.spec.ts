import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  commentThreads,
  findingDetail as detail,
  findingId as id,
  myCommentReports,
  myReport,
  postedComment,
} from './finding-detail.fixtures';
import { FindingDetailStore, TOP_COMPOSER_KEY } from './finding-detail.store';
import { FindingDetailDto } from './finding-detail.service';

describe('FindingDetailStore', () => {
  let store: InstanceType<typeof FindingDetailStore>;
  let httpMock: HttpTestingController;
  let snackBar: { open: ReturnType<typeof vi.fn> };

  const otherId = '0d4f9a3e-2222-4222-8333-444455556666';

  const expectDetailRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}`);
  const expectCommentsRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}/comments`);
  const expectMyReportRequest = (findingId: string) =>
    httpMock.expectOne({ method: 'GET', url: `/api/findings/${findingId}/my-report` });
  const expectMyCommentReportsRequest = (findingId: string) =>
    httpMock.expectOne({
      method: 'GET',
      url: `/api/findings/${findingId}/comments/my-reports`,
    });

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

  it('starts loading with no finding, no discussion, and no report states', () => {
    expect(store.status()).toBe('loading');
    expect(store.finding()).toBeNull();
    expect(store.comments()).toBeNull();
    expect(store.myReport()).toBeNull();
    expect(store.myCommentReports()).toBeNull();
    expect(store.commentReportPendingId()).toBeNull();
  });

  it('load requests the finding, its discussion, and both report states in parallel', () => {
    store.load(id);

    expectDetailRequest(id);
    expectCommentsRequest(id);
    expectMyReportRequest(id);
    expectMyCommentReportsRequest(id);
  });

  it('stays loading until all four answers are in — the finding alone is not enough', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expect(store.status()).toBe('loading');

    expectCommentsRequest(id).flush(commentThreads());
    expect(store.status()).toBe('loading');

    expectMyReportRequest(id).flush(myReport());
    expect(store.status()).toBe('loading');

    expectMyCommentReportsRequest(id).flush(myCommentReports());
    expect(store.status()).toBe('loaded');
    expect(store.finding()).toEqual(detail());
    expect(store.comments()).toEqual(commentThreads());
    expect(store.myReport()).toBe(false);
  });

  it('stays loading until all four answers are in — the discussion and reports alone are not enough', () => {
    store.load(id);

    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());
    expect(store.status()).toBe('loading');

    expectDetailRequest(id).flush(detail());
    expect(store.status()).toBe('loaded');
  });

  it('surfaces an already-reported finding right from the load', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport({ reported: true }));
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.myReport()).toBe(true);
  });

  it('surfaces already-reported comments right from the load — replies included', () => {
    // From the fixtures: grace's top-level comment and linus's reply are already reported.
    const reportedIds = [commentThreads()[0].id, commentThreads()[0].replies[0].id];
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports({ reportedCommentIds: reportedIds }));

    expect(store.myCommentReports()).toEqual(reportedIds);
  });

  it('keeps the threads exactly as the server ordered them', () => {
    store.load(id);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.comments()?.map((t) => t.id)).toEqual(commentThreads().map((t) => t.id));
    expect(store.comments()?.[0].replies.map((r) => r.id)).toEqual(
      commentThreads()[0].replies.map((r) => r.id),
    );
  });

  it('a 404 on the finding puts the store in the not-found state, distinct from a load error', () => {
    store.load(id);

    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectMyReportRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectMyCommentReportsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });

    expect(store.status()).toBe('notFound');
    expect(store.finding()).toBeNull();
  });

  it('a 404 on the discussion alone is still not-found — the finding is gone either way', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.status()).toBe('notFound');
  });

  it('a 404 on my report alone is still not-found — only an unknown finding answers it', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.status()).toBe('notFound');
  });

  it('a 404 on the comment reports alone is still not-found — only an unknown finding answers it', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });

    expect(store.status()).toBe('notFound');
  });

  it('a failing finding request puts the store in the error state', () => {
    store.load(id);

    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.status()).toBe('error');
  });

  it('a failing discussion request is a load error even when the finding arrived', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.status()).toBe('error');
  });

  it('a failing my-report request is a load error even when the rest arrived', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    expect(store.status()).toBe('error');
  });

  it('a failing comment-reports request is a load error even when the rest arrived', () => {
    store.load(id);

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
  });

  it('a finding request that never answers times out into the error state', () => {
    vi.useFakeTimers();
    try {
      store.load(id);
      expectCommentsRequest(id).flush(commentThreads());
      expectMyReportRequest(id).flush(myReport());
      expectMyCommentReportsRequest(id).flush(myCommentReports());

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
      expectMyReportRequest(id).flush(myReport());
      expectMyCommentReportsRequest(id).flush(myCommentReports());

      vi.advanceTimersByTime(5000);
      expect(store.status()).toBe('error');
    } finally {
      vi.useRealTimers();
    }
  });

  it('retry re-requests the finding, the discussion, and my report state for the id that failed', () => {
    store.load(id);
    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());

    store.retry();

    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    expectMyCommentReportsRequest(id).flush(myCommentReports());
    expect(store.status()).toBe('loaded');
    expect(store.finding()).toEqual(detail());
    expect(store.comments()).toEqual(commentThreads());
  });

  it('loading a different id replaces the finding, the discussion, and my report state', () => {
    store.load(id);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport({ reported: true }));
    expectMyCommentReportsRequest(id).flush(
      myCommentReports({ reportedCommentIds: [commentThreads()[0].id] }),
    );

    store.load(otherId);
    expectDetailRequest(otherId).flush(detail({ id: otherId, title: 'Another finding' }));
    expectCommentsRequest(otherId).flush([]);
    expectMyReportRequest(otherId).flush(myReport());
    expectMyCommentReportsRequest(otherId).flush(myCommentReports());

    expect(store.finding()?.id).toBe(otherId);
    expect(store.finding()?.title).toBe('Another finding');
    expect(store.comments()).toEqual([]);
    expect(store.myReport()).toBe(false);
    expect(store.myCommentReports()).toEqual([]);
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
      expectMyReportRequest(id).flush(myReport());
      expectMyCommentReportsRequest(id).flush(myCommentReports());
    };

    it('voting on a comment without a vote PUTs the chosen direction', () => {
      loadDiscussion();

      store.voteOnComment({ commentId: freshThread().id, direction: 'up' });

      const req = expectVoteRequest(freshThread().id);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ direction: 'up' });
    });

    it('the response reconciles exactly that comment in place — no refetch', () => {
      loadDiscussion();

      store.voteOnComment({ commentId: freshThread().id, direction: 'up' });
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

      store.voteOnComment({ commentId: votedUpThread().id, direction: 'up' });

      const req = expectVoteRequest(votedUpThread().id);
      expect(req.request.method).toBe('DELETE');

      req.flush({ upvoteCount: 11, downvoteCount: 2, myVote: null });
      expect(store.comments()?.[0].upvoteCount).toBe(11);
      expect(store.comments()?.[0].myVote).toBeNull();
    });

    it('clicking the other side switches with a single PUT', () => {
      loadDiscussion();

      store.voteOnComment({ commentId: votedUpThread().id, direction: 'down' });

      const req = expectVoteRequest(votedUpThread().id);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ direction: 'down' });
    });

    it('a vote on a reply reconciles inside its thread', () => {
      loadDiscussion();

      // The reply currently holds a down vote, so choosing up is a switch.
      store.voteOnComment({ commentId: votedDownReply().id, direction: 'up' });
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

      store.voteOnComment({ commentId: freshThread().id, direction: 'up' });
      expect(store.pendingCommentVoteIds()).toContain(freshThread().id);

      expectVoteRequest(freshThread().id).flush({ upvoteCount: 4, downvoteCount: 4, myVote: 'up' });
      expect(store.pendingCommentVoteIds()).toEqual([]);
    });

    it('a failed vote announces itself in a snackbar and leaves the discussion untouched', () => {
      loadDiscussion();

      store.voteOnComment({ commentId: freshThread().id, direction: 'up' });
      expectVoteRequest(freshThread().id).flush('boom', {
        status: 500,
        statusText: 'Server Error',
      });

      expect(store.comments()).toEqual(commentThreads());
      expect(store.pendingCommentVoteIds()).toEqual([]);
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain("Couldn't vote on comment");
    });
  });

  describe('voting on the finding (issue #15)', () => {
    // A finding the stub user did not author, so its vote controls are live.
    const votable = (overrides: Partial<FindingDetailDto> = {}) =>
      detail({ author: 'grace_hopper', myVote: null, ...overrides });

    const expectVoteRequest = () => httpMock.expectOne(`/api/findings/${id}/my-vote`);

    const loadFinding = (finding = votable()) => {
      store.load(id);
      expectDetailRequest(id).flush(finding);
      expectCommentsRequest(id).flush(commentThreads());
      expectMyReportRequest(id).flush(myReport());
      expectMyCommentReportsRequest(id).flush(myCommentReports());
    };

    it('digging a finding with no vote PUTs a dig', () => {
      loadFinding();

      store.voteOnFinding({ type: 'dig' });

      const req = expectVoteRequest();
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ type: 'dig' });
    });

    it('burying PUTs a bury carrying the chosen reason', () => {
      loadFinding();

      store.voteOnFinding({ type: 'bury', reason: 'spam' });

      const req = expectVoteRequest();
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ type: 'bury', reason: 'spam' });
    });

    it('clicking the side already held withdraws it with a DELETE', () => {
      loadFinding(votable({ myVote: 'dig' }));

      store.voteOnFinding({ type: 'dig' });

      const req = expectVoteRequest();
      expect(req.request.method).toBe('DELETE');
    });

    it('clicking the other side switches with a single PUT', () => {
      loadFinding(votable({ myVote: 'dig' }));

      store.voteOnFinding({ type: 'bury', reason: 'duplicate' });

      const req = expectVoteRequest();
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ type: 'bury', reason: 'duplicate' });
    });

    it('reconciles the dig count and highlight from the response — no refetch', () => {
      loadFinding(votable({ digCount: 123 }));

      store.voteOnFinding({ type: 'dig' });
      expectVoteRequest().flush({ digCount: 124, myVote: 'dig' });

      expect(store.finding()?.digCount).toBe(124);
      expect(store.finding()?.myVote).toBe('dig');
      httpMock.expectNone(`/api/findings/${id}`);
    });

    it('marks the finding vote pending while its request is in flight, and only then', () => {
      loadFinding();

      store.voteOnFinding({ type: 'dig' });
      expect(store.pendingFindingVote()).toBe(true);

      expectVoteRequest().flush({ digCount: 124, myVote: 'dig' });
      expect(store.pendingFindingVote()).toBe(false);
    });

    it('a failed finding vote announces itself in a snackbar and leaves the finding untouched', () => {
      const finding = votable({ digCount: 123 });
      loadFinding(finding);

      store.voteOnFinding({ type: 'dig' });
      expectVoteRequest().flush('boom', { status: 500, statusText: 'Server Error' });

      expect(store.finding()).toEqual(finding);
      expect(store.pendingFindingVote()).toBe(false);
      expect(snackBar.open).toHaveBeenCalled();
    });
  });

  describe('posting comments (issue #17)', () => {
    const threadId = () => commentThreads()[0].id;
    const expectPostRequest = () => httpMock.expectOne(`/api/findings/${id}/comments`);

    const loadPage = () => {
      store.load(id);
      expectDetailRequest(id).flush(detail({ commentCount: 9 }));
      expectCommentsRequest(id).flush(commentThreads());
      expectMyReportRequest(id).flush(myReport());
      expectMyCommentReportsRequest(id).flush(myCommentReports());
    };

    it('starts with only the permanent top composer, empty and idle', () => {
      expect(store.composers()).toEqual({ top: { draft: '', pending: false } });
    });

    describe('the top-level composer', () => {
      it('posting sends the draft with no parent', () => {
        loadPage();
        store.updateComposerDraft({ composerKey: TOP_COMPOSER_KEY, text: 'A fresh take.' });

        store.postComment(TOP_COMPOSER_KEY);

        const req = expectPostRequest();
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual({ text: 'A fresh take.', parentCommentId: null });
      });

      it('a successful post pins the new comment first, clears the draft, and counts it', () => {
        loadPage();
        store.updateComposerDraft({ composerKey: TOP_COMPOSER_KEY, text: 'A fresh take.' });

        store.postComment(TOP_COMPOSER_KEY);
        expectPostRequest().flush(postedComment(), { status: 201, statusText: 'Created' });

        // Pinned to the top for this session — the real best-first position applies from
        // the next load — rendered straight from the response, replies empty.
        expect(store.comments()![0]).toEqual({ ...postedComment(), replies: [] });
        expect(store.comments()!.length).toBe(commentThreads().length + 1);
        expect(store.composers()[TOP_COMPOSER_KEY]).toEqual({ draft: '', pending: false });
        // The count reconciles by local +1 — the contract event is the server's business.
        expect(store.finding()!.commentCount).toBe(10);
        httpMock.expectNone(`/api/findings/${id}`);
      });

      it('a second post pins above the first — newest post first', () => {
        loadPage();
        store.updateComposerDraft({ composerKey: TOP_COMPOSER_KEY, text: 'First thought.' });
        store.postComment(TOP_COMPOSER_KEY);
        expectPostRequest().flush(postedComment({ id: 'c-first', text: 'First thought.' }), {
          status: 201,
          statusText: 'Created',
        });

        store.updateComposerDraft({ composerKey: TOP_COMPOSER_KEY, text: 'Second thought.' });
        store.postComment(TOP_COMPOSER_KEY);
        expectPostRequest().flush(postedComment({ id: 'c-second', text: 'Second thought.' }), {
          status: 201,
          statusText: 'Created',
        });

        expect(store.comments()![0].text).toBe('Second thought.');
        expect(store.comments()![1].text).toBe('First thought.');
      });
    });

    describe('reply composers', () => {
      it('opening creates independent, empty composer state for the thread', () => {
        loadPage();

        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });

        expect(store.composers()[threadId()]).toEqual({ draft: '', pending: false });
        // The top composer is untouched.
        expect(store.composers()[TOP_COMPOSER_KEY]).toEqual({ draft: '', pending: false });
      });

      it("opening for an answered reply appends '@author ' to the draft — nothing typed is lost", () => {
        loadPage();
        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        store.updateComposerDraft({ composerKey: threadId(), text: 'Good point' });

        store.openReplyComposer({ threadId: threadId(), appendAuthor: 'linus_t' });

        const draft = store.composers()[threadId()].draft;
        expect(draft).toContain('Good point');
        expect(draft).toMatch(/@linus_t\s$/);
      });

      it('cancelling closes the composer and discards the draft', () => {
        loadPage();
        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        store.updateComposerDraft({ composerKey: threadId(), text: 'Half a thought' });

        store.closeOrResetComposer(threadId());
        expect(store.composers()[threadId()]).toBeUndefined();

        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        expect(store.composers()[threadId()].draft).toBe('');
      });

      it('posting sends the draft with the thread as parent', () => {
        loadPage();
        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        store.updateComposerDraft({ composerKey: threadId(), text: 'An answer.' });

        store.postComment(threadId());

        const req = expectPostRequest();
        expect(req.request.body).toEqual({ text: 'An answer.', parentCommentId: threadId() });
      });

      it('a successful reply appends to its thread chronologically, closes the composer, and counts it', () => {
        loadPage();
        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        store.updateComposerDraft({ composerKey: threadId(), text: 'An answer.' });

        store.postComment(threadId());
        expectPostRequest().flush(postedComment({ text: 'An answer.' }), {
          status: 201,
          statusText: 'Created',
        });

        const replies = store.comments()![0].replies;
        expect(replies[replies.length - 1]).toEqual(postedComment({ text: 'An answer.' }));
        expect(replies.length).toBe(commentThreads()[0].replies.length + 1);
        expect(store.composers()[threadId()]).toBeUndefined();
        expect(store.finding()!.commentCount).toBe(10);
      });
    });

    describe('in-flight and failure', () => {
      it('posting marks only that composer pending — composers never block each other', () => {
        loadPage();
        store.updateComposerDraft({ composerKey: TOP_COMPOSER_KEY, text: 'A fresh take.' });
        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        store.updateComposerDraft({ composerKey: threadId(), text: 'An answer.' });

        store.postComment(threadId());

        expect(store.composers()[threadId()].pending).toBe(true);
        expect(store.composers()[TOP_COMPOSER_KEY].pending).toBe(false);

        // The top composer can post while the reply is still in flight: two requests open.
        store.postComment(TOP_COMPOSER_KEY);
        const open = httpMock.match(`/api/findings/${id}/comments`);
        expect(open.length).toBe(2);
        open.forEach((req) => req.flush(postedComment(), { status: 201, statusText: 'Created' }));
      });

      it('a failed post announces itself and preserves the draft for another try', () => {
        loadPage();
        store.updateComposerDraft({ composerKey: TOP_COMPOSER_KEY, text: 'A fresh take.' });

        store.postComment(TOP_COMPOSER_KEY);
        expectPostRequest().flush('boom', { status: 500, statusText: 'Server Error' });

        expect(store.composers()[TOP_COMPOSER_KEY]).toEqual({
          draft: 'A fresh take.',
          pending: false,
        });
        expect(store.comments()).toEqual(commentThreads());
        expect(store.finding()!.commentCount).toBe(9);
        expect(snackBar.open).toHaveBeenCalled();
      });

      it('a failed reply keeps its composer open with the draft intact', () => {
        loadPage();
        store.openReplyComposer({ threadId: threadId(), appendAuthor: null });
        store.updateComposerDraft({ composerKey: threadId(), text: 'An answer.' });

        store.postComment(threadId());
        expectPostRequest().flush('boom', { status: 500, statusText: 'Server Error' });

        expect(store.composers()[threadId()]).toEqual({ draft: 'An answer.', pending: false });
        expect(snackBar.open).toHaveBeenCalled();
      });
    });
  });

  describe('reporting the finding (issue #32)', () => {
    // From the statute fixture's reportable conduct rules.
    const spamPointId = 'aaaa0000-0000-4000-8000-000000000002';

    // A finding the stub user did not author, so its report action is live.
    const reportable = () => detail({ author: 'grace_hopper' });

    const expectFileRequest = () =>
      httpMock.expectOne({ method: 'POST', url: `/api/findings/${id}/my-report` });

    const loadReportable = (report = myReport()) => {
      store.load(id);
      expectDetailRequest(id).flush(reportable());
      expectCommentsRequest(id).flush(commentThreads());
      expectMyReportRequest(id).flush(report);
      expectMyCommentReportsRequest(id).flush(myCommentReports());
    };

    it('filing POSTs the cited point and note to my-report', () => {
      loadReportable();

      store.fileFindingReport({ statutePointId: spamPointId, note: 'Links a spam farm.' });

      const req = expectFileRequest();
      expect(req.request.body).toEqual({
        statutePointId: spamPointId,
        note: 'Links a spam farm.',
      });
    });

    it('a successful filing marks the finding reported and confirms in a snackbar', () => {
      loadReportable();

      store.fileFindingReport({ statutePointId: spamPointId, note: null });
      expectFileRequest().flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });

      expect(store.myReport()).toBe(true);
      expect(store.reportPending()).toBe(false);
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain('Report submitted');
    });

    it('marks the report pending while its request is in flight, and only then', () => {
      loadReportable();

      store.fileFindingReport({ statutePointId: spamPointId, note: null });
      expect(store.reportPending()).toBe(true);

      expectFileRequest().flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });
      expect(store.reportPending()).toBe(false);
    });

    it('a second filing while one is in flight is ignored — a single request goes out', () => {
      loadReportable();

      store.fileFindingReport({ statutePointId: spamPointId, note: null });
      store.fileFindingReport({ statutePointId: spamPointId, note: null });

      const open = httpMock.match({ method: 'POST', url: `/api/findings/${id}/my-report` });
      expect(open.length).toBe(1);
      open[0].flush(myReport({ reported: true }), { status: 201, statusText: 'Created' });
    });

    it('the duplicate refusal also marks the finding reported — the server already holds my report', () => {
      loadReportable();

      store.fileFindingReport({ statutePointId: spamPointId, note: null });
      expectFileRequest().flush(
        { type: 'podkop:problem:already-reported' },
        { status: 409, statusText: 'Conflict' },
      );

      expect(store.myReport()).toBe(true);
      expect(store.reportPending()).toBe(false);
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain('already reported');
    });

    it('any other failure leaves the state untouched and announces itself', () => {
      loadReportable();

      store.fileFindingReport({ statutePointId: spamPointId, note: null });
      expectFileRequest().flush('boom', { status: 500, statusText: 'Server Error' });

      expect(store.myReport()).toBe(false);
      expect(store.reportPending()).toBe(false);
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain("Couldn't submit the report");
    });

    it('filing never touches the finding itself — no score, no vote, no refetch (ADR 0008)', () => {
      loadReportable();
      const before = store.finding();

      store.fileFindingReport({ statutePointId: spamPointId, note: null });
      expectFileRequest().flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });

      expect(store.finding()).toEqual(before);
      httpMock.expectNone(`/api/findings/${id}`);
    });
  });

  describe('reporting a comment (issue #33)', () => {
    // From the statute fixture's reportable conduct rules.
    const spamPointId = 'aaaa0000-0000-4000-8000-000000000002';

    // From the fixtures: grace's top-level comment and linus's reply — both authored by
    // someone else, so their report actions are live.
    const targetId = () => commentThreads()[0].id;
    const replyId = () => commentThreads()[0].replies[0].id;

    const expectFileRequest = (commentId: string) =>
      httpMock.expectOne({ method: 'POST', url: `/api/comments/${commentId}/my-report` });

    const loadDiscussion = (reports = myCommentReports()) => {
      store.load(id);
      expectDetailRequest(id).flush(detail());
      expectCommentsRequest(id).flush(commentThreads());
      expectMyReportRequest(id).flush(myReport());
      expectMyCommentReportsRequest(id).flush(reports);
    };

    it('filing POSTs the cited point and note to the comment my-report endpoint', () => {
      loadDiscussion();

      store.fileCommentReport({
        commentId: targetId(),
        statutePointId: spamPointId,
        note: 'Spam in the discussion.',
      });

      const req = expectFileRequest(targetId());
      expect(req.request.body).toEqual({
        statutePointId: spamPointId,
        note: 'Spam in the discussion.',
      });
    });

    it('a successful filing adds the comment to my reported comments and confirms in a snackbar', () => {
      loadDiscussion();

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      expectFileRequest(targetId()).flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });

      expect(store.myCommentReports()).toContain(targetId());
      expect(store.commentReportPendingId()).toBeNull();
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain('Report submitted');
    });

    it('a reply is reported the same way, by its own id', () => {
      loadDiscussion();

      store.fileCommentReport({ commentId: replyId(), statutePointId: spamPointId, note: null });
      expectFileRequest(replyId()).flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });

      expect(store.myCommentReports()).toContain(replyId());
    });

    it('a fresh filing joins comments already reported at load — nothing is forgotten', () => {
      loadDiscussion(myCommentReports({ reportedCommentIds: [replyId()] }));

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      expectFileRequest(targetId()).flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });

      expect(store.myCommentReports()).toContain(targetId());
      expect(store.myCommentReports()).toContain(replyId());
    });

    it('names the comment whose filing is in flight while it runs, and only then', () => {
      loadDiscussion();

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      expect(store.commentReportPendingId()).toBe(targetId());

      expectFileRequest(targetId()).flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });
      expect(store.commentReportPendingId()).toBeNull();
    });

    it('a second filing while one is in flight is ignored — a single request goes out', () => {
      loadDiscussion();

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });

      const open = httpMock.match({
        method: 'POST',
        url: `/api/comments/${targetId()}/my-report`,
      });
      expect(open.length).toBe(1);
      open[0].flush(myReport({ reported: true }), { status: 201, statusText: 'Created' });
    });

    it('the duplicate refusal also marks the comment reported — the server already holds my report', () => {
      loadDiscussion();

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      expectFileRequest(targetId()).flush(
        { type: 'podkop:problem:already-reported' },
        { status: 409, statusText: 'Conflict' },
      );

      expect(store.myCommentReports()).toContain(targetId());
      expect(store.commentReportPendingId()).toBeNull();
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain('already reported');
    });

    it('any other failure leaves the state untouched and announces itself', () => {
      loadDiscussion();

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      expectFileRequest(targetId()).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(store.myCommentReports()).toEqual([]);
      expect(store.commentReportPendingId()).toBeNull();
      expect(snackBar.open).toHaveBeenCalled();
      expect(String(snackBar.open.mock.calls[0]?.[0])).toContain("Couldn't submit the report");
    });

    it('filing never touches the discussion or the finding — no counts, no refetch (ADR 0008)', () => {
      loadDiscussion();
      const findingBefore = store.finding();

      store.fileCommentReport({ commentId: targetId(), statutePointId: spamPointId, note: null });
      expectFileRequest(targetId()).flush(myReport({ reported: true }), {
        status: 201,
        statusText: 'Created',
      });

      expect(store.comments()).toEqual(commentThreads());
      expect(store.finding()).toEqual(findingBefore);
      httpMock.expectNone(`/api/findings/${id}`);
      httpMock.expectNone(`/api/findings/${id}/comments`);
    });
  });
});
