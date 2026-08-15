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
});
