import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { privacyPolicy } from '../documents.fixtures';
import { PrivacyPolicyPage } from './privacy-policy-page';

@Component({ template: 'main page' })
class MainPageStub {}

describe('PrivacyPolicyPage', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;

  const element = (): HTMLElement => harness.routeNativeElement!;
  const expectPolicyRequest = () => httpMock.expectOne('/api/privacy-policy');

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: MainPageStub },
          { path: 'privacy-policy', component: PrivacyPolicyPage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  it('landing on the route requests the current privacy policy', async () => {
    await harness.navigateByUrl('/privacy-policy', PrivacyPolicyPage);

    const req = expectPolicyRequest();
    expect(req.request.method).toBe('GET');
  });

  it('shows a loading state until the policy arrives', async () => {
    await harness.navigateByUrl('/privacy-policy', PrivacyPolicyPage);

    const req = expectPolicyRequest();
    harness.detectChanges();
    expect(element().querySelector('.document-state.loading mat-spinner')).not.toBeNull();
    expect(element().querySelector('.section-title')).toBeNull();

    req.flush(privacyPolicy());
    harness.detectChanges();

    expect(element().querySelector('.document-state.loading')).toBeNull();
    expect(element().querySelector('.section-title')).not.toBeNull();
  });

  it('renders the version line, sections, and paragraphs in server order', async () => {
    await harness.navigateByUrl('/privacy-policy', PrivacyPolicyPage);
    expectPolicyRequest().flush(privacyPolicy());
    harness.detectChanges();

    const versionLine = element().querySelector<HTMLElement>('.version-line');
    expect(versionLine?.textContent).toContain('Version 1');
    expect(versionLine?.textContent).toContain('2026');

    const titles = Array.from(element().querySelectorAll<HTMLElement>('.section-title')).map(
      (t) => t.textContent ?? '',
    );
    expect(titles).toHaveLength(2);
    expect(titles[0]).toContain('Data we process');
    expect(titles[1]).toContain('Your rights');

    const paragraphs = Array.from(element().querySelectorAll<HTMLElement>('.paragraph')).map(
      (p) => p.textContent ?? '',
    );
    expect(paragraphs).toHaveLength(3);
    expect(paragraphs[0]).toContain('We store the findings, comments, and votes you submit.');
    expect(paragraphs[1]).toContain('We do not track you across other sites.');
    expect(paragraphs[2]).toContain('You may request the erasure of your account.');
  });

  it('shows the error state when the policy cannot be loaded', async () => {
    await harness.navigateByUrl('/privacy-policy', PrivacyPolicyPage);
    expectPolicyRequest().flush('broken', { status: 500, statusText: 'Internal Server Error' });
    harness.detectChanges();

    const error = element().querySelector<HTMLElement>('.document-state.error');
    expect(error?.textContent).toContain("Couldn't load the Privacy Policy.");
    expect(element().querySelector('.section-title')).toBeNull();
  });
});
