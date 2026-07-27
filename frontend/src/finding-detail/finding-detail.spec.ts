import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { findingDetail as detail, findingId as id } from './finding-detail.fixtures';
import { FindingDetail } from './finding-detail';

@Component({ template: 'main page' })
class MainPageStub {}

describe('FindingDetail', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;
  let router: Router;

  const expectDetailRequest = (findingId: string) => httpMock.expectOne(`/api/findings/${findingId}`);

  const element = (): HTMLElement => harness.routeNativeElement!;

  beforeEach(async () => {
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

  it('landing on the route fetches the finding named in the URL', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);

    const req = expectDetailRequest(id);
    expect(req.request.method).toBe('GET');
  });

  it('shows a spinner while the finding is loading', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);

    expect(element().querySelector('.detail-state.loading mat-spinner')).not.toBeNull();
  });

  it('renders the finding once it arrives', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush(detail());
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

  it('omits the promoted timestamp for a finding that was never promoted', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush(detail({ promotedAt: null }));
    harness.detectChanges();

    expect(element().querySelector('.promoted-at')).toBeNull();
    expect(element().querySelector('.created-at')?.textContent).toContain('2026');
  });

  it('renders the thumbnail when the finding has one', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush(detail());
    harness.detectChanges();

    const img = element().querySelector<HTMLImageElement>('img.thumbnail');
    expect(img?.getAttribute('src')).toBe('https://example.com/thumb.jpg');
    expect(element().querySelector('.thumbnail-placeholder')).toBeNull();
  });

  it('renders a neutral placeholder when the finding has no thumbnail', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush(detail({ thumbnailUrl: null }));
    harness.detectChanges();

    expect(element().querySelector('img.thumbnail')).toBeNull();
    expect(element().querySelector('.thumbnail-placeholder')).not.toBeNull();
  });

  it('an unknown finding shows a not-found state with a way back and no retry', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });
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
    harness.detectChanges();

    element().querySelector<HTMLAnchorElement>('.back-link')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
  });

  it('a load failure shows an inline error whose Retry re-requests the finding', async () => {
    await harness.navigateByUrl(`/finding/${id}`, FindingDetail);
    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    const error = element().querySelector('.detail-state.error');
    expect(error?.textContent).toContain("Couldn't load the finding.");

    element().querySelector<HTMLButtonElement>('.retry-button')?.click();
    expectDetailRequest(id);
  });
});
