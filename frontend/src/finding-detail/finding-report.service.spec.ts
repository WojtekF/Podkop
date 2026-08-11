import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FindingReportService, MyReportDto } from './finding-report.service';
import { findingId as id, myReport } from './finding-detail.fixtures';

describe('FindingReportService', () => {
  let service: FindingReportService;
  let httpMock: HttpTestingController;

  const endpoint = `/api/findings/${id}/my-report`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(FindingReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the my-report state from the finding my-report endpoint', () => {
    let received: MyReportDto | undefined;
    service.getMyReport(id).subscribe((state) => (received = state));

    const req = httpMock.expectOne(endpoint);
    expect(req.request.method).toBe('GET');

    req.flush(myReport({ reported: true }));
    expect(received).toEqual(myReport({ reported: true }));
  });

  it('POSTs the cited point and note to file a report', () => {
    let received: MyReportDto | undefined;
    service
      .fileReport(id, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: 'Links a spam farm.',
      })
      .subscribe((state) => (received = state));

    const req = httpMock.expectOne(endpoint);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
      note: 'Links a spam farm.',
    });

    req.flush(myReport({ reported: true }), { status: 201, statusText: 'Created' });
    expect(received).toEqual(myReport({ reported: true }));
  });

  it('a report without a note POSTs an explicit null note', () => {
    service
      .fileReport(id, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: null,
      })
      .subscribe();

    const req = httpMock.expectOne(endpoint);
    expect(req.request.body).toEqual({
      statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
      note: null,
    });
  });

  it('propagates the duplicate refusal as an error the caller can inspect', () => {
    let status: number | undefined;
    service
      .fileReport(id, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: null,
      })
      .subscribe({
        error: (e: { status?: number }) => (status = e.status),
      });

    httpMock
      .expectOne(endpoint)
      .flush(
        { type: 'podkop:problem:already-reported' },
        { status: 409, statusText: 'Conflict' },
      );

    expect(status).toBe(409);
  });
});
