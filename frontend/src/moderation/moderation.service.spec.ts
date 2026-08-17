import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { caseQueue } from './moderation.fixtures';
import { CaseSummaryDto, ModerationService } from './moderation.service';

describe('ModerationService', () => {
  let service: ModerationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ModerationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('requests the case queue and relays it exactly as served', () => {
    let cases: CaseSummaryDto[] | undefined;
    service.getCaseQueue().subscribe((served) => (cases = served));

    const req = httpMock.expectOne('/api/moderation/cases');
    expect(req.request.method).toBe('GET');

    req.flush(caseQueue());

    expect(cases).toEqual(caseQueue());
  });

  it('dismissCase POSTs the Dismissed verdict at the case it names', () => {
    let completed = false;
    // The Comment target from the fixtures — the target's kind must reach the URL as-is.
    service
      .dismissCase('Comment', 'c0000000-0000-4000-8000-000000000009')
      .subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne(
      '/api/moderation/cases/Comment/c0000000-0000-4000-8000-000000000009/verdict',
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ verdict: 'Dismissed' });

    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(completed).toBe(true);
  });
});
