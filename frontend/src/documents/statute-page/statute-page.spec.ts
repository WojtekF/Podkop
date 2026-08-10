import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { statute } from '../documents.fixtures';
import { StatutePage } from './statute-page';

@Component({ template: 'main page' })
class MainPageStub {}

describe('StatutePage', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;

  const element = (): HTMLElement => harness.routeNativeElement!;
  const expectStatuteRequest = () => httpMock.expectOne('/api/statute');

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: MainPageStub },
          { path: 'statute', component: StatutePage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  it('landing on the route requests the current statute', async () => {
    await harness.navigateByUrl('/statute', StatutePage);

    const req = expectStatuteRequest();
    expect(req.request.method).toBe('GET');
  });

  it('shows a loading state until the statute arrives', async () => {
    await harness.navigateByUrl('/statute', StatutePage);

    const req = expectStatuteRequest();
    harness.detectChanges();
    expect(element().querySelector('.document-state.loading mat-spinner')).not.toBeNull();
    expect(element().querySelector('.section-title')).toBeNull();

    req.flush(statute());
    harness.detectChanges();

    expect(element().querySelector('.document-state.loading')).toBeNull();
    expect(element().querySelector('.section-title')).not.toBeNull();
  });

  it('renders the version line, sections, and numbered points in server order', async () => {
    await harness.navigateByUrl('/statute', StatutePage);
    expectStatuteRequest().flush(statute());
    harness.detectChanges();

    const versionLine = element().querySelector<HTMLElement>('.version-line');
    expect(versionLine?.textContent).toContain('Version 2');
    expect(versionLine?.textContent).toContain('2026');

    const titles = Array.from(element().querySelectorAll<HTMLElement>('.section-title')).map(
      (t) => t.textContent ?? '',
    );
    expect(titles).toHaveLength(3);
    expect(titles[0]).toContain('Purpose of the service');
    expect(titles[1]).toContain('Rules of conduct');
    expect(titles[2]).toContain('Consequences');

    // The citation is composed section.point — point 1 of section 2 reads "2.1". This exact
    // form is what a Report will later cite, so it is asserted verbatim.
    const numbers = Array.from(element().querySelectorAll<HTMLElement>('.point-number')).map(
      (n) => n.textContent?.trim(),
    );
    expect(numbers).toEqual(['1.1', '2.1', '2.2', '3.1']);

    const texts = Array.from(element().querySelectorAll<HTMLElement>('.point-text')).map(
      (t) => t.textContent ?? '',
    );
    expect(texts[0]).toContain('Podkop is a community for sharing and judging findings.');
    expect(texts[1]).toContain('Do not post spam.');
  });

  it('shows the error state when the statute cannot be loaded', async () => {
    await harness.navigateByUrl('/statute', StatutePage);
    expectStatuteRequest().flush('broken', { status: 500, statusText: 'Internal Server Error' });
    harness.detectChanges();

    const error = element().querySelector<HTMLElement>('.document-state.error');
    expect(error?.textContent).toContain("Couldn't load the Statute.");
    expect(element().querySelector('.section-title')).toBeNull();
  });
});
