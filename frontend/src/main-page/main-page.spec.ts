import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { FeedPage, FindingSummary } from './main-page-feed.service';
import { MainPage } from './main-page';

describe('MainPage', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;
  let router: Router;

  const summary = (title: string): FindingSummary => ({
    id: crypto.randomUUID(),
    title,
    description: `${title} — description`,
    sourceUrl: 'https://example.com/articles/1',
    domain: 'example.com',
    thumbnailUrl: null,
    author: 'grace_hopper',
    tags: ['dotnet'],
    digCount: 100,
    commentCount: 10,
    promotedAt: '2026-07-08T10:00:00Z',
  });

  const pageOf = (titles: string[], hasNextPage: boolean): FeedPage => ({
    items: titles.map(summary),
    hasNextPage,
  });

  const expectFeedRequest = (page: number) =>
    httpMock.expectOne((r) => {
      const params = new URL(r.urlWithParams, 'http://test').searchParams;
      return r.url.startsWith('/api/findings') && params.get('page') === String(page);
    });

  const element = (): HTMLElement => harness.routeNativeElement!;
  const button = (selector: string) => element().querySelector<HTMLButtonElement>(selector);

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '', component: MainPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  it('landing on the route fetches the page named in the URL', async () => {
    await harness.navigateByUrl('/?page=3', MainPage);

    const req = expectFeedRequest(3);
    expect(req.request.method).toBe('GET');
  });

  it('landing without a page param fetches page 1', async () => {
    await harness.navigateByUrl('/', MainPage);

    expectFeedRequest(1);
  });

  it('an invalid page param falls back to page 1', async () => {
    await harness.navigateByUrl('/?page=potato', MainPage);

    expectFeedRequest(1);
  });

  it('shows a centered spinner while the page is loading', async () => {
    await harness.navigateByUrl('/', MainPage);

    expect(element().querySelector('.feed-state.loading mat-spinner')).not.toBeNull();
  });

  it('renders one FindingCard per item once the page arrives', async () => {
    await harness.navigateByUrl('/', MainPage);
    expectFeedRequest(1).flush(pageOf(['A', 'B'], false));
    harness.detectChanges();

    expect(element().querySelectorAll('main-page-finding-card').length).toBe(2);
    expect(element().querySelector('.feed-state.loading')).toBeNull();
  });

  it('disables Previous on page 1 and Next on the last page', async () => {
    await harness.navigateByUrl('/', MainPage);
    expectFeedRequest(1).flush(pageOf(['A'], false));
    harness.detectChanges();

    expect(button('.previous-page-button')?.disabled).toBe(true);
    expect(button('.next-page-button')?.disabled).toBe(true);
  });

  it('enables both buttons in the middle of the feed', async () => {
    await harness.navigateByUrl('/?page=2', MainPage);
    expectFeedRequest(2).flush(pageOf(['A'], true));
    harness.detectChanges();

    expect(button('.previous-page-button')?.disabled).toBe(false);
    expect(button('.next-page-button')?.disabled).toBe(false);
  });

  it('clicking Next navigates to the next ?page= URL and fetches it', async () => {
    await harness.navigateByUrl('/', MainPage);
    expectFeedRequest(1).flush(pageOf(['A'], true));
    harness.detectChanges();

    button('.next-page-button')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/?page=2');
    expectFeedRequest(2);
  });

  it('clicking Previous from page 2 navigates back to the clean URL for page 1', async () => {
    await harness.navigateByUrl('/?page=2', MainPage);
    expectFeedRequest(2).flush(pageOf(['A'], true));
    harness.detectChanges();

    button('.previous-page-button')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
    expectFeedRequest(1);
  });

  it('a failed fetch shows an inline error whose Retry re-requests the page', async () => {
    await harness.navigateByUrl('/', MainPage);
    expectFeedRequest(1).flush('boom', { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(element().querySelector('.feed-state.error')).not.toBeNull();

    button('.retry-button')?.click();
    expectFeedRequest(1);
  });

  it('an empty feed on page 1 explains itself without a first-page action', async () => {
    await harness.navigateByUrl('/', MainPage);
    expectFeedRequest(1).flush(pageOf([], false));
    harness.detectChanges();

    const empty = element().querySelector('.feed-state.empty');
    expect(empty?.textContent).toContain('No findings have been promoted yet.');
    expect(button('.first-page-button')).toBeNull();
  });

  it('a stale deep link past the end offers Go to first page, which navigates and fetches', async () => {
    await harness.navigateByUrl('/?page=9', MainPage);
    expectFeedRequest(9).flush(pageOf([], false));
    harness.detectChanges();

    const firstPageButton = button('.first-page-button');
    expect(firstPageButton).not.toBeNull();

    firstPageButton?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
    expectFeedRequest(1);
  });
});
