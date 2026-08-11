import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { CommentThreadDto } from './finding-comments.service';
import { FindingDetailDto } from './finding-detail.service';
import {
  commentThreads,
  findingDetail as detail,
  findingId as id,
  myReport,
  postedComment,
} from './finding-detail.fixtures';
import { MyReportDto } from './finding-report.service';
import { statute } from '../documents/documents.fixtures';
import { FindingDetail } from './finding-detail';

@Component({ template: 'main page' })
class MainPageStub {}

describe('FindingDetail', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;
  let router: Router;

  // jsdom has no layout, so "centered in the viewport" is observed at the DOM seam: which
  // element was asked to scroll itself into view, and how.
  let scrollIntoViewCalls: { element: Element; options: unknown }[];

  const expectDetailRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}`);
  const expectCommentsRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}/comments`);
  const expectMyReportRequest = (findingId: string) =>
    httpMock.expectOne({ method: 'GET', url: `/api/findings/${findingId}/my-report` });

  const flushAll = (
    detailBody = detail(),
    threads: CommentThreadDto[] = commentThreads(),
    report: MyReportDto = myReport(),
  ) => {
    expectDetailRequest(id).flush(detailBody);
    expectCommentsRequest(id).flush(threads);
    expectMyReportRequest(id).flush(report);
  };

  const element = (): HTMLElement => harness.routeNativeElement!;

  beforeEach(async () => {
    scrollIntoViewCalls = [];
    Element.prototype.scrollIntoView = function (options?: boolean | ScrollIntoViewOptions) {
      scrollIntoViewCalls.push({ element: this, options });
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: '', component: MainPageStub },
            { path: 'finding/:id', component: FindingDetail },
          ],
          withComponentInputBinding(),
        ),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  it('landing on the route requests the finding, its discussion, and my report state in parallel', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);

    const detailReq = expectDetailRequest(id);
    const commentsReq = expectCommentsRequest(id);
    const myReportReq = expectMyReportRequest(id);
    expect(detailReq.request.method).toBe('GET');
    expect(commentsReq.request.method).toBe('GET');
    expect(myReportReq.request.method).toBe('GET');
  });

  it('shows one spinner covering the whole page until ALL THREE answers arrive', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);

    const myReportReq = expectMyReportRequest(id);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush(commentThreads());
    harness.detectChanges();

    // The finding and its discussion are not enough — nothing renders while the my-report
    // state is in flight.
    expect(element().querySelector('.detail-state.loading mat-spinner')).not.toBeNull();
    expect(element().querySelector('.title')).toBeNull();

    myReportReq.flush(myReport());
    harness.detectChanges();

    expect(element().querySelector('.detail-state.loading')).toBeNull();
    expect(element().querySelector('.title')).not.toBeNull();
  });

  it('renders the finding once everything arrives', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll();
    harness.detectChanges();

    const title = element().querySelector<HTMLElement>('.title');
    expect(title?.textContent).toContain('A remarkable finding');
    const titleLink = title?.closest('a') ?? title?.querySelector('a');
    expect(titleLink?.getAttribute('href')).toBe('https://blog.example.org/posts/42');
    expect(titleLink?.getAttribute('target')).toBe('_blank');
    expect(titleLink?.getAttribute('rel')).toBe('noopener noreferrer');

    expect(element().querySelector('.source-link')).toBeNull();
    const domain = element().querySelector<HTMLElement>('.domain');
    expect(domain?.textContent).toContain('blog.example.org');
    expect(domain?.closest('a')).toBeNull();
    expect(element().querySelector('.description')?.textContent).toContain(
      'The full, untruncated description of the finding.',
    );
    expect(element().querySelector('.author')?.textContent).toContain('ada_lovelace');

    const tags = Array.from(element().querySelectorAll('.tag')).map((t) => t.textContent);
    expect(tags.join(' ')).toContain('#angular');
    expect(tags.join(' ')).toContain('#webdev');

    expect(element().querySelector('.created-at')?.textContent).toContain('2026');
    expect(element().querySelector('.promoted-at')?.textContent).toContain('2026');

    expect(element().querySelector('.detail-state.loading')).toBeNull();
  });

  it('renders the discussion: threads in server order, replies nested one level under their parent', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll();
    harness.detectChanges();

    const comments = element().querySelector('#comments');
    expect(comments).not.toBeNull();

    const threadEls = comments!.querySelectorAll('.comment-thread');
    expect(threadEls.length).toBe(2);

    const first = threadEls[0];
    const topRow = first.querySelector('.comment');
    expect(topRow).not.toBeNull();
    expect(topRow!.closest('.replies')).toBeNull();
    expect(topRow!.querySelector('.author')?.textContent).toContain('grace_hopper');
    expect(topRow!.querySelector('.text')?.textContent).toContain('Best take in the thread.');
    expect(topRow!.querySelector('.upvote-count')?.textContent).toContain('12');
    expect(topRow!.querySelector('.downvote-count')?.textContent).toContain('2');
    expect(topRow!.querySelector('.age')?.textContent?.trim()).toBeTruthy();

    const replyAuthors = Array.from(first.querySelectorAll('.replies .comment .author')).map(
      (author) => author.textContent?.trim(),
    );
    expect(replyAuthors).toEqual(['linus_t', 'ada_lovelace']);

    expect(threadEls[1].querySelector('.author')?.textContent).toContain('margaret_h');
    expect(threadEls[1].querySelectorAll('.replies .comment').length).toBe(0);
  });

  it('a finding with no discussion still shows the comments section, empty', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll(detail(), []);
    harness.detectChanges();

    expect(element().querySelector('#comments')).not.toBeNull();
    expect(element().querySelectorAll('.comment-thread').length).toBe(0);
  });

  it("landing via a card's comment-count link centers the first comment in the viewport", async () => {
    await harness.navigateByUrl(`/finding/${id}#comments`, FindingDetail);
    flushAll();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(scrollIntoViewCalls.length).toBeGreaterThan(0);
    const lastCall = scrollIntoViewCalls[scrollIntoViewCalls.length - 1];
    // The element sent to the viewport's center belongs to the FIRST thread.
    expect(lastCall.element.closest('.comment-thread')).toBe(
      element().querySelectorAll('.comment-thread')[0],
    );
    expect(lastCall.options).toMatchObject({ block: 'center' });
  });

  it('landing without the comments fragment leaves the page unscrolled', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(scrollIntoViewCalls.length).toBe(0);
  });

  it('omits the promoted timestamp for a finding that was never promoted', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll(detail({ promotedAt: null }));
    harness.detectChanges();

    expect(element().querySelector('.promoted-at')).toBeNull();
    expect(element().querySelector('.created-at')?.textContent).toContain('2026');
  });

  it('renders the thumbnail when the finding has one', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll();
    harness.detectChanges();

    const img = element().querySelector<HTMLImageElement>('img.thumbnail');
    expect(img?.getAttribute('src')).toBe('https://example.com/thumb.jpg');
    expect(element().querySelector('.thumbnail-placeholder')).toBeNull();
  });

  it('renders a neutral placeholder when the finding has no thumbnail', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushAll(detail({ thumbnailUrl: null }));
    harness.detectChanges();

    expect(element().querySelector('img.thumbnail')).toBeNull();
    expect(element().querySelector('.thumbnail-placeholder')).not.toBeNull();
  });

  it('an unknown finding shows a not-found state with a way back and no retry', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectMyReportRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    const notFound = element().querySelector('.detail-state.not-found');
    expect(notFound?.textContent).toContain('Finding not found');

    const back = element().querySelector<HTMLAnchorElement>('.back-link');
    expect(back).not.toBeNull();
    expect(back?.textContent).toContain('Main Page');

    expect(element().querySelector('.retry-button')).toBeNull();
  });

  it('the not-found way back returns to the Main Page', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectMyReportRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    element().querySelector<HTMLAnchorElement>('.back-link')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
  });

  it('a load failure shows an inline error whose Retry re-requests all three answers', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectCommentsRequest(id).flush(commentThreads());
    expectMyReportRequest(id).flush(myReport());
    harness.detectChanges();

    const error = element().querySelector('.detail-state.error');
    expect(error?.textContent).toContain("Couldn't load the finding.");

    element().querySelector<HTMLButtonElement>('.retry-button')?.click();
    expectDetailRequest(id);
    expectCommentsRequest(id);
    expectMyReportRequest(id);
  });

  it('a failing discussion request is a load failure too, even when the finding arrived', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectMyReportRequest(id).flush(myReport());
    harness.detectChanges();

    expect(element().querySelector('.detail-state.error')).not.toBeNull();
    expect(element().querySelector('.title')).toBeNull();
  });

  describe('voting on comments (issue #18)', () => {
    // margaret_h's thread — the second one — carries no vote yet.
    const freshThread = () => commentThreads()[1];
    const expectVoteRequest = (commentId: string) =>
      httpMock.expectOne(`/api/comments/${commentId}/my-vote`);

    const threadEls = () => element().querySelectorAll('.comment-thread');
    const freshThreadUpButton = () =>
      threadEls()[1].querySelector<HTMLButtonElement>('button.upvote-button');

    const loadPage = async () => {
      await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
      flushAll();
      harness.detectChanges();
    };

    it("shows the reader's existing votes highlighted right after a plain load", async () => {
      await loadPage();

      // grace_hopper's top-level comment is voted up; linus_t's reply is voted down.
      const topRow = threadEls()[0].querySelector('.comment')!;
      expect(topRow.querySelector('button.upvote-button.voted')).not.toBeNull();
      expect(topRow.querySelector('button.downvote-button.voted')).toBeNull();

      const reply = threadEls()[0].querySelectorAll('.replies .comment')[0];
      expect(reply.querySelector('button.downvote-button.voted')).not.toBeNull();

      // margaret_h's thread holds no vote — nothing on it is highlighted.
      expect(threadEls()[1].querySelector('.voted')).toBeNull();
    });

    it('clicking upvote PUTs the vote and shows the reconciled counts and highlight', async () => {
      await loadPage();

      freshThreadUpButton()!.click();
      const req = expectVoteRequest(freshThread().id);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ direction: 'up' });

      req.flush({ upvoteCount: 4, downvoteCount: 4, myVote: 'up' });
      harness.detectChanges();

      const row = threadEls()[1].querySelector('.comment')!;
      expect(row.querySelector('.upvote-count')?.textContent).toContain('4');
      expect(row.querySelector('button.upvote-button.voted')).not.toBeNull();
    });

    it('a failed vote leaves the visible counts and highlight unchanged', async () => {
      await loadPage();

      freshThreadUpButton()!.click();
      expectVoteRequest(freshThread().id).flush('boom', {
        status: 500,
        statusText: 'Server Error',
      });
      harness.detectChanges();

      const row = threadEls()[1].querySelector('.comment')!;
      expect(row.querySelector('.upvote-count')?.textContent).toContain('3');
      expect(row.querySelector('.downvote-count')?.textContent).toContain('4');
      expect(threadEls()[1].querySelector('.voted')).toBeNull();
    });
  });

  describe('voting on the finding (issue #15)', () => {
    // A finding the stub user did not author, so its vote controls are live.
    const votable = (overrides: Partial<FindingDetailDto> = {}) =>
      detail({ author: 'grace_hopper', myVote: null, ...overrides });
    const digButton = () => element().querySelector<HTMLButtonElement>('button.dig-button');
    const buryButton = () => element().querySelector<HTMLButtonElement>('button.bury-button');
    const digCount = () => element().querySelector<HTMLElement>('.dig-count');
    const expectVoteRequest = () => httpMock.expectOne(`/api/findings/${id}/my-vote`);

    const loadPage = async (finding = votable()) => {
      await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
      flushAll(finding);
      harness.detectChanges();
    };

    it("shows the reader's existing dig highlighted right after a plain load", async () => {
      await loadPage(votable({ myVote: 'dig' }));

      expect(digButton()?.classList.contains('voted')).toBe(true);
      expect(buryButton()?.classList.contains('voted')).toBe(false);
    });

    it('shows the dig count on the dig control and no count on the bury control', async () => {
      await loadPage(votable({ digCount: 123 }));

      expect(digCount()?.textContent).toContain('123');
      expect(buryButton()?.textContent).not.toMatch(/\d/);
    });

    it('clicking dig PUTs the vote and shows the reconciled count and highlight', async () => {
      await loadPage(votable({ digCount: 123 }));

      digButton()!.click();
      const req = expectVoteRequest();
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ type: 'dig' });

      req.flush({ digCount: 124, myVote: 'dig' });
      harness.detectChanges();

      expect(digCount()?.textContent).toContain('124');
      expect(digButton()?.classList.contains('voted')).toBe(true);
    });

    it("disables the vote controls on the reader's own finding", async () => {
      await loadPage(detail({ author: 'ada_lovelace', myVote: null }));

      expect(digButton()?.disabled).toBe(true);
      expect(buryButton()?.disabled).toBe(true);
    });

    it('clicking the highlighted bury control withdraws the vote with a DELETE', async () => {
      // The reader already holds a bury, so the click is an undo: no reason picker, straight
      // to the wire. The response clears the highlight.
      await loadPage(votable({ myVote: 'bury', digCount: 123 }));

      buryButton()!.click();
      const req = expectVoteRequest();
      expect(req.request.method).toBe('DELETE');

      req.flush({ digCount: 123, myVote: null });
      harness.detectChanges();

      expect(buryButton()?.classList.contains('voted')).toBe(false);
    });

    it('disables the vote controls while a vote request is in flight, and only then', async () => {
      await loadPage(votable());

      digButton()!.click();
      harness.detectChanges();

      // In flight: both controls sit out the round trip.
      expect(digButton()?.disabled).toBe(true);
      expect(buryButton()?.disabled).toBe(true);

      expectVoteRequest().flush({ digCount: 124, myVote: 'dig' });
      harness.detectChanges();

      // ...and come back once it lands.
      expect(digButton()?.disabled).toBe(false);
      expect(buryButton()?.disabled).toBe(false);
    });

    it('a failed finding vote leaves the visible count and highlight unchanged', async () => {
      await loadPage(votable({ digCount: 123 }));

      digButton()!.click();
      expectVoteRequest().flush('boom', { status: 500, statusText: 'Server Error' });
      harness.detectChanges();

      expect(digCount()?.textContent).toContain('123');
      expect(digButton()?.classList.contains('voted')).toBe(false);
    });
  });

  describe('writing comments (issue #17)', () => {
    const comments = () => element().querySelector('#comments')!;
    const threadEls = () => element().querySelectorAll('.comment-thread');
    const topComposer = () => comments().querySelector('app-comment-composer');
    const composerIn = (scope: Element) => scope.querySelector('app-comment-composer');
    const textareaOf = (composer: Element) =>
      composer.querySelector<HTMLTextAreaElement>('textarea.comment-text');
    const postButtonOf = (composer: Element) =>
      composer.querySelector<HTMLButtonElement>('button.post-button');
    const replyButtonOf = (scope: Element) =>
      scope.querySelector<HTMLButtonElement>('button.reply-button');
    const expectPostRequest = () => httpMock.expectOne(`/api/findings/${id}/comments`);

    const loadPage = async () => {
      await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
      flushAll();
      harness.detectChanges();
    };

    const type = async (composer: Element, text: string) => {
      const area = textareaOf(composer)!;
      area.value = text;
      area.dispatchEvent(new Event('input'));
      await harness.fixture.whenStable();
      harness.detectChanges();
    };

    it('the composer sits at the top of the comments section, before every thread', async () => {
      await loadPage();

      const composer = topComposer();
      expect(composer).not.toBeNull();
      const firstThread = element().querySelector('.comment-thread')!;
      // Document order: the composer precedes the first thread.
      expect(
        composer!.compareDocumentPosition(firstThread) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    });

    it('posting from the top composer sends the text and renders the new comment first, cleared', async () => {
      await loadPage();
      await type(topComposer()!, 'A fresh take.');

      postButtonOf(topComposer()!)!.click();
      const req = expectPostRequest();
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ text: 'A fresh take.', parentCommentId: null });

      req.flush(postedComment(), { status: 201, statusText: 'Created' });
      harness.detectChanges();

      // Pinned first for this session; the composer is empty again; no refetch happened.
      expect(threadEls()[0].textContent).toContain('A fresh take.');
      expect(textareaOf(topComposer()!)!.value).toBe('');
      httpMock.expectNone(`/api/findings/${id}`);
      httpMock.expectNone(`/api/findings/${id}/comments?`);
    });

    it('reply on a top-level comment opens an empty composer under that thread', async () => {
      await loadPage();

      // margaret_h's thread — the second one.
      replyButtonOf(threadEls()[1].querySelector('.comment')!)!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();

      const inline = composerIn(threadEls()[1]);
      expect(inline).not.toBeNull();
      expect(textareaOf(inline!)!.value).toBe('');
      // Only that thread gained a composer.
      expect(composerIn(threadEls()[0])).toBeNull();
    });

    it("reply on a reply targets the same thread with the answered author's @name in the draft", async () => {
      await loadPage();

      // linus_t's reply lives in the first thread.
      const replyRow = threadEls()[0].querySelectorAll('.replies .comment')[0];
      replyButtonOf(replyRow)!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();

      const inline = composerIn(threadEls()[0]);
      expect(inline).not.toBeNull();
      expect(textareaOf(inline!)!.value).toMatch(/@linus_t\s$/);
    });

    it('posting a reply sends the thread as parent and appends it under the thread, composer closed', async () => {
      await loadPage();
      replyButtonOf(threadEls()[1].querySelector('.comment')!)!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      await type(composerIn(threadEls()[1])!, 'An answer.');

      postButtonOf(composerIn(threadEls()[1])!)!.click();
      const req = expectPostRequest();
      expect(req.request.body).toEqual({
        text: 'An answer.',
        parentCommentId: commentThreads()[1].id,
      });

      req.flush(postedComment({ text: 'An answer.' }), { status: 201, statusText: 'Created' });
      harness.detectChanges();

      const replyTexts = Array.from(
        threadEls()[1].querySelectorAll('.replies .comment .text'),
      ).map((text) => text.textContent?.trim());
      expect(replyTexts[replyTexts.length - 1]).toContain('An answer.');
      expect(composerIn(threadEls()[1])).toBeNull();
    });

    it('cancel closes the inline composer and discards the draft', async () => {
      await loadPage();
      replyButtonOf(threadEls()[1].querySelector('.comment')!)!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      await type(composerIn(threadEls()[1])!, 'Half a thought');

      composerIn(threadEls()[1])!
        .querySelector<HTMLButtonElement>('button.cancel-button')!
        .click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      expect(composerIn(threadEls()[1])).toBeNull();

      replyButtonOf(threadEls()[1].querySelector('.comment')!)!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      expect(textareaOf(composerIn(threadEls()[1])!)!.value).toBe('');
    });

    it('an in-flight reply disables only its own composer', async () => {
      await loadPage();
      replyButtonOf(threadEls()[1].querySelector('.comment')!)!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      await type(composerIn(threadEls()[1])!, 'An answer.');

      postButtonOf(composerIn(threadEls()[1])!)!.click();
      harness.detectChanges();

      expect(textareaOf(composerIn(threadEls()[1])!)!.disabled).toBe(true);
      // The top composer sits out nothing.
      expect(textareaOf(topComposer()!)!.disabled).toBe(false);

      expectPostRequest().flush(postedComment({ text: 'An answer.' }), {
        status: 201,
        statusText: 'Created',
      });
    });

    it('a failed post keeps the composer text so nothing typed is ever lost', async () => {
      await loadPage();
      await type(topComposer()!, 'A fresh take.');

      postButtonOf(topComposer()!)!.click();
      expectPostRequest().flush('boom', { status: 500, statusText: 'Server Error' });
      harness.detectChanges();

      expect(textareaOf(topComposer()!)!.value).toBe('A fresh take.');
      expect(textareaOf(topComposer()!)!.disabled).toBe(false);
    });
  });

  describe('reporting the finding (issue #32)', () => {
    // A finding the stub user did not author, so its report action is live.
    const reportable = (overrides: Partial<FindingDetailDto> = {}) =>
      detail({ author: 'grace_hopper', ...overrides });

    const reportButton = () =>
      element().querySelector<HTMLButtonElement>('button.report-button');
    // The dialog may render outside the page's own subtree (e.g. in an overlay), so it is
    // looked up on the document.
    const dialog = () => document.querySelector<HTMLElement>('.report-dialog');

    const loadPage = async (finding = reportable(), report = myReport()) => {
      await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
      flushAll(finding, commentThreads(), report);
      harness.detectChanges();
    };

    const openDialog = async () => {
      reportButton()!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();
    };

    afterEach(() => {
      // Dialogs opened into an overlay outlive the fixture unless cleaned up here.
      document.querySelectorAll('.report-dialog').forEach((el) => el.remove());
    });

    it('offers a Report action on a loaded finding', async () => {
      await loadPage();

      expect(reportButton()).not.toBeNull();
      expect(reportButton()?.textContent).toContain('Report');
      expect(reportButton()?.disabled).toBe(false);
    });

    it("disables the report action on the reader's own finding", async () => {
      await loadPage(detail({ author: 'ada_lovelace' }));

      expect(reportButton()?.disabled).toBe(true);
    });

    it('shows the already-reported state right after a plain load', async () => {
      await loadPage(reportable(), myReport({ reported: true }));

      expect(reportButton()?.disabled).toBe(true);
      expect(reportButton()?.textContent).toContain('Reported');
      expect(reportButton()?.classList.contains('reported')).toBe(true);
    });

    it('clicking Report opens the report dialog, which asks for the current Statute', async () => {
      await loadPage();

      await openDialog();

      expect(dialog()).not.toBeNull();
      httpMock.expectOne('/api/statute');
    });

    it('cancelling the dialog closes it and files nothing', async () => {
      await loadPage();
      await openDialog();
      httpMock.expectOne('/api/statute').flush(statute());
      await harness.fixture.whenStable();
      harness.detectChanges();

      dialog()!.querySelector<HTMLButtonElement>('button.cancel-report-button')!.click();
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(dialog()).toBeNull();
      httpMock.expectNone({ method: 'POST', url: `/api/findings/${id}/my-report` });
      expect(reportButton()?.disabled).toBe(false);
    });

    it('submitting the dialog files the report, closes it, and marks the finding reported', async () => {
      await loadPage();
      await openDialog();
      httpMock.expectOne('/api/statute').flush(statute());
      await harness.fixture.whenStable();
      harness.detectChanges();

      dialog()!.querySelectorAll<HTMLElement>('.report-point')[0].click();
      await harness.fixture.whenStable();
      harness.detectChanges();
      dialog()!.querySelector<HTMLButtonElement>('button.submit-report-button')!.click();

      const req = httpMock.expectOne({
        method: 'POST',
        url: `/api/findings/${id}/my-report`,
      });
      expect(req.request.body).toEqual({
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: null,
      });

      req.flush(myReport({ reported: true }), { status: 201, statusText: 'Created' });
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(dialog()).toBeNull();
      expect(reportButton()?.textContent).toContain('Reported');
      expect(reportButton()?.disabled).toBe(true);
    });
  });
});
