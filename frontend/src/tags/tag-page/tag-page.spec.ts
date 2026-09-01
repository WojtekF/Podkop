import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TagPage } from './tag-page';
import { card, contentId, ref, tagPage } from '../tags.fixtures';

@Component({ template: 'main page' })
class MainPageStub {}

describe('TagPage', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;
  let router: Router;

  const expectTagRequest = (name: string, page: number) =>
    httpMock.expectOne((r) => {
      const params = new URL(r.urlWithParams, 'http://test').searchParams;
      return r.url === `/api/tags/${name}` && params.get('page') === String(page);
    });

  const expectBatchRequest = () =>
    httpMock.expectOne((r) => r.url.startsWith('/api/findings/batch'));

  /** Lands on a tag page and takes it all the way to its loaded state. */
  const loadedWith = async (url: string, name = 'dotnet', page = 1) => {
    await harness.navigateByUrl(url, TagPage);
    expectTagRequest(name, page).flush(tagPage([ref(1), ref(2)]));
    expectBatchRequest().flush([card(1), card(2)]);
    harness.detectChanges();
  };

  const element = (): HTMLElement => harness.routeNativeElement!;
  const control = (selector: string) => element().querySelector<HTMLElement>(selector);

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: MainPageStub },
          { path: 'tag/:name', component: TagPage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  it('landing on the route fetches the tag named in the URL', async () => {
    await harness.navigateByUrl('/tag/dotnet', TagPage);

    const req = expectTagRequest('dotnet', 1);
    expect(req.request.method).toBe('GET');
  });

  it('landing without a page param fetches page 1', async () => {
    await harness.navigateByUrl('/tag/dotnet', TagPage);

    expectTagRequest('dotnet', 1);
  });

  it('fetches the page named in the URL', async () => {
    await harness.navigateByUrl('/tag/dotnet?page=3', TagPage);

    expectTagRequest('dotnet', 3);
  });

  it('an invalid page param falls back to page 1', async () => {
    await harness.navigateByUrl('/tag/dotnet?page=potato', TagPage);

    expectTagRequest('dotnet', 1);
  });

  it('a non-positive page param falls back to page 1', async () => {
    await harness.navigateByUrl('/tag/dotnet?page=0', TagPage);

    expectTagRequest('dotnet', 1);
  });

  it('a name in any casing is sent through as the URL spelled it', async () => {
    // Folding is the server's job — the page must not pre-normalise and risk disagreeing about
    // what the canonical form is.
    await harness.navigateByUrl('/tag/DotNet', TagPage);

    expectTagRequest('DotNet', 1);
  });

  it('shows a centered spinner while the page is loading', async () => {
    await harness.navigateByUrl('/tag/dotnet', TagPage);

    expect(element().querySelector('.tag-state.loading mat-spinner')).not.toBeNull();
  });

  it('heads the page with the bare tag name and no hash', async () => {
    await loadedWith('/tag/dotnet');

    const header = control('.tag-header');
    expect(header?.textContent).toContain('dotnet');
    expect(header?.textContent).not.toContain('#');
  });

  it('renders one finding card per hydrated item', async () => {
    await loadedWith('/tag/dotnet');

    expect(element().querySelectorAll('main-page-finding-card').length).toBe(2);
  });

  it('renders the stream in the order the tag page returned', async () => {
    await harness.navigateByUrl('/tag/dotnet', TagPage);
    expectTagRequest('dotnet', 1).flush(tagPage([ref(3), ref(1)]));
    expectBatchRequest().flush([card(1), card(3)]);
    harness.detectChanges();

    const titles = Array.from(element().querySelectorAll('.title-link')).map((a) => a.textContent);
    expect(titles).toEqual(['Finding 3', 'Finding 1']);
  });

  it('shows an empty state when the tag exists but this page lists nothing', async () => {
    await harness.navigateByUrl('/tag/dotnet?page=9', TagPage);
    expectTagRequest('dotnet', 9).flush(tagPage([]));
    harness.detectChanges();

    expect(control('.tag-state.empty')?.textContent).toContain('Nothing here yet.');
  });

  it('offers all three type filters with the combined stream selected by default', async () => {
    await loadedWith('/tag/dotnet');

    expect(control('.type-filter.filter-all')?.textContent).toContain('All');
    expect(control('.type-filter.filter-findings')?.textContent).toContain('Findings');
    expect(control('.type-filter.filter-entries')?.textContent).toContain('Entries');
    expect(control('.type-filter.filter-all')?.classList.contains('selected')).toBe(true);
  });

  it('marks the filter the URL names as selected', async () => {
    await harness.navigateByUrl('/tag/dotnet?type=findings', TagPage);
    expectTagRequest('dotnet', 1).flush(tagPage([]));
    harness.detectChanges();

    expect(control('.type-filter.filter-findings')?.classList.contains('selected')).toBe(true);
    expect(control('.type-filter.filter-all')?.classList.contains('selected')).toBe(false);
  });

  it('sends the type filter the URL names', async () => {
    await harness.navigateByUrl('/tag/dotnet?type=entries', TagPage);

    const req = expectTagRequest('dotnet', 1);
    expect(new URL(req.request.urlWithParams, 'http://test').searchParams.get('type')).toBe(
      'entries',
    );
  });

  it('an unrecognised type param falls back to the combined stream', async () => {
    await harness.navigateByUrl('/tag/dotnet?type=photos', TagPage);

    const req = expectTagRequest('dotnet', 1);
    expect(new URL(req.request.urlWithParams, 'http://test').searchParams.get('type')).toBe('all');
  });

  it('choosing a filter puts it in the URL and starts again at page 1', async () => {
    await harness.navigateByUrl('/tag/dotnet?page=3', TagPage);
    expectTagRequest('dotnet', 3).flush(tagPage([]));
    harness.detectChanges();

    control('.type-filter.filter-findings')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toContain('type=findings');
    expect(router.url).not.toContain('page=3');
  });

  it('the Entries filter loads like any other and simply lists nothing yet', async () => {
    // Full type model from day one: it lights up when the Microblog slice lands, with no rework.
    await harness.navigateByUrl('/tag/dotnet?type=entries', TagPage);
    expectTagRequest('dotnet', 1).flush(tagPage([]));
    harness.detectChanges();

    expect(control('.tag-state.empty')).not.toBeNull();
    expect(control('.tag-state.not-found')).toBeNull();
  });

  it('Next turns to the following page through the URL', async () => {
    await harness.navigateByUrl('/tag/dotnet', TagPage);
    expectTagRequest('dotnet', 1).flush(tagPage([ref(1)], true));
    expectBatchRequest().flush([card(1)]);
    harness.detectChanges();

    control('.next-page')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toContain('page=2');
  });

  it('Previous turns back a page through the URL', async () => {
    await harness.navigateByUrl('/tag/dotnet?page=2', TagPage);
    expectTagRequest('dotnet', 2).flush(tagPage([ref(1)]));
    expectBatchRequest().flush([card(1)]);
    harness.detectChanges();

    control('.previous-page')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toContain('/tag/dotnet');
    expect(router.url).not.toContain('page=2');
  });

  it('an unknown tag shows a not-found state with a way back and no retry', async () => {
    await harness.navigateByUrl('/tag/nosuchtag', TagPage);
    expectTagRequest('nosuchtag', 1).flush('missing', { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    const notFound = control('.tag-state.not-found');
    expect(notFound?.textContent).toContain('Tag not found');
    expect(control('.back-link')?.textContent).toContain('Main Page');
    expect(control('.retry-button')).toBeNull();
  });

  it('the not-found state heads and filters nothing — there is no tag to head', async () => {
    await harness.navigateByUrl('/tag/nosuchtag', TagPage);
    expectTagRequest('nosuchtag', 1).flush('missing', { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    expect(control('.tag-header')).toBeNull();
    expect(control('.type-filter')).toBeNull();
  });

  it('the not-found way back returns to the Main Page', async () => {
    await harness.navigateByUrl('/tag/nosuchtag', TagPage);
    expectTagRequest('nosuchtag', 1).flush('missing', { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    control('.back-link')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
  });

  it('a load failure shows an error state with a retry that re-requests the page', async () => {
    await harness.navigateByUrl('/tag/dotnet', TagPage);
    expectTagRequest('dotnet', 1).flush('boom', { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(control('.tag-state.error')?.textContent).toContain("Couldn't load the tag page.");

    control('.retry-button')?.click();
    harness.detectChanges();

    expectTagRequest('dotnet', 1);
  });
});
