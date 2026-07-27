import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { CommentThreadDto } from './finding-comments.service';
import { commentThreads, findingDetail as detail, findingId as id } from './finding-detail.fixtures';
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

  const expectDetailRequest = (findingId: string) => httpMock.expectOne(`/api/findings/${findingId}`);
  const expectCommentsRequest = (findingId: string) =>
    httpMock.expectOne(`/api/findings/${findingId}/comments`);

  const flushBoth = (detailBody = detail(), threads: CommentThreadDto[] = commentThreads()) => {
    expectDetailRequest(id).flush(detailBody);
    expectCommentsRequest(id).flush(threads);
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

  it('landing on the route requests the finding and its discussion in parallel', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);

    const detailReq = expectDetailRequest(id);
    const commentsReq = expectCommentsRequest(id);
    expect(detailReq.request.method).toBe('GET');
    expect(commentsReq.request.method).toBe('GET');
  });

  it('shows one spinner covering the whole page until BOTH the finding and its discussion arrive', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);

    const commentsReq = expectCommentsRequest(id);
    expectDetailRequest(id).flush(detail());
    harness.detectChanges();

    // The finding alone is not enough — nothing renders while the discussion is in flight.
    expect(element().querySelector('.detail-state.loading mat-spinner')).not.toBeNull();
    expect(element().querySelector('.title')).toBeNull();

    commentsReq.flush(commentThreads());
    harness.detectChanges();

    expect(element().querySelector('.detail-state.loading')).toBeNull();
    expect(element().querySelector('.title')).not.toBeNull();
  });

  it('renders the finding once everything arrives', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushBoth();
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
    flushBoth();
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
    flushBoth(detail(), []);
    harness.detectChanges();

    expect(element().querySelector('#comments')).not.toBeNull();
    expect(element().querySelectorAll('.comment-thread').length).toBe(0);
  });

  it("landing via a card's comment-count link centers the first comment in the viewport", async () => {
    await harness.navigateByUrl(`/finding/${id}#comments`, FindingDetail);
    flushBoth();
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
    flushBoth();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(scrollIntoViewCalls.length).toBe(0);
  });

  it('omits the promoted timestamp for a finding that was never promoted', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushBoth(detail({ promotedAt: null }));
    harness.detectChanges();

    expect(element().querySelector('.promoted-at')).toBeNull();
    expect(element().querySelector('.created-at')?.textContent).toContain('2026');
  });

  it('renders the thumbnail when the finding has one', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushBoth();
    harness.detectChanges();

    const img = element().querySelector<HTMLImageElement>('img.thumbnail');
    expect(img?.getAttribute('src')).toBe('https://example.com/thumb.jpg');
    expect(element().querySelector('.thumbnail-placeholder')).toBeNull();
  });

  it('renders a neutral placeholder when the finding has no thumbnail', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    flushBoth(detail({ thumbnailUrl: null }));
    harness.detectChanges();

    expect(element().querySelector('img.thumbnail')).toBeNull();
    expect(element().querySelector('.thumbnail-placeholder')).not.toBeNull();
  });

  it('an unknown finding shows a not-found state with a way back and no retry', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
    expectCommentsRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
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
    harness.detectChanges();

    element().querySelector<HTMLAnchorElement>('.back-link')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
  });

  it('a load failure shows an inline error whose Retry re-requests the finding AND the discussion', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    expectCommentsRequest(id).flush(commentThreads());
    harness.detectChanges();

    const error = element().querySelector('.detail-state.error');
    expect(error?.textContent).toContain("Couldn't load the finding.");

    element().querySelector<HTMLButtonElement>('.retry-button')?.click();
    expectDetailRequest(id);
    expectCommentsRequest(id);
  });

  it('a failing discussion request is a load failure too, even when the finding arrived', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush(detail());
    expectCommentsRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(element().querySelector('.detail-state.error')).not.toBeNull();
    expect(element().querySelector('.title')).toBeNull();
  });
});
